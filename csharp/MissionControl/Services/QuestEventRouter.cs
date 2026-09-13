using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

/// <summary>
/// Intercepts quest completion events via /client/game/profile/items/moving.
/// Three responsibilities:
/// 1. Remove completed quests from SelectedQuestIds (free the slot)
/// 2. Re-run the slot selection immediately so freed slots are refilled without
///    waiting for the next /client/quest/list fetch (reboot / raid end)
/// 3. Rewrite the response's newly unlocked quests: hide sequels that weren't
///    selected, inject the fresh picks so they show up in the Tasks screen live
/// </summary>
[Injectable]
public sealed class QuestEventRouter(
    JsonUtil jsonUtil,
    ProfileStateStorage storage,
    ConfigService configService,
    QuestSelectionService selection,
    ISptLogger<QuestEventRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ItemEventRouterRequest>(
        "/client/game/profile/items/moving",
        (url, requestData, sessionId, output, cancellationToken) =>
            ProcessQuestEvents(sessionId, output, requestData, storage, configService, selection, logger)
    )
])
{
    private const int AvailableForStart = 1;

    private static ValueTask<string> ProcessQuestEvents(
        MongoId sessionId,
        string? output,
        ItemEventRouterRequest? requestData,
        ProfileStateStorage storage,
        ConfigService configService,
        QuestSelectionService selection,
        ISptLogger<QuestEventRouter> logger)
    {
        if (string.IsNullOrEmpty(output) || requestData?.Data == null)
            return ValueTask.FromResult(output ?? string.Empty);

        // Check if this request contains a quest completion
        // 4.1: Data entries are raw JsonElements; match on the Action name and read qid directly
        var completedQuestIds = new List<string>();
        foreach (var action in requestData.Data)
        {
            if (action.ValueKind != JsonValueKind.Object) continue;
            if (!action.TryGetProperty("Action", out var actionProp) ||
                !string.Equals(actionProp.GetString(), ItemEventActions.QUEST_COMPLETE, StringComparison.Ordinal))
                continue;

            if (action.TryGetProperty("qid", out var qidProp) && qidProp.GetString() is { } qid)
                completedQuestIds.Add(qid);
        }

        if (completedQuestIds.Count == 0)
            return ValueTask.FromResult(output ?? string.Empty);

        try
        {
            var sessionIdStr = sessionId.ToString();
            var state = storage.Load(sessionIdStr);
            var debug = configService.Config.debug;

            // Remove completed quests from selected slots
            foreach (var questId in completedQuestIds)
            {
                if (state.SelectedQuestIds.Remove(questId) && debug)
                    logger.Info($"[MissionControl] Quest {questId[..8]}... completed — slot freed");
            }

            // Re-run the selection on the live quest pool (the profile already reflects the
            // completion at this point, so sequels are AvailableForStart here).
            var picks = selection.Run(sessionId, state);
            storage.Save(sessionIdStr, state);

            // Build the set of quest IDs that should remain visible in the response
            var allowedQuestIds = new HashSet<string>(picks.VisibleIds);
            allowedQuestIds.UnionWith(picks.ExemptIds);
            // Also allow the completed quests themselves (their status change must reach the client)
            foreach (var qid in completedQuestIds)
                allowedQuestIds.Add(qid);

            var node = JsonNode.Parse(output);
            if (node == null)
                return ValueTask.FromResult(output ?? string.Empty);

            var profileChanges = node["data"]?["profileChanges"]?.AsObject()
                ?? node["profileChanges"]?.AsObject();
            if (profileChanges == null)
                return ValueTask.FromResult(output ?? string.Empty);

            // Filter the response: remove newly unlocked quests that aren't allowed
            int removed = 0;
            foreach (var profileProp in profileChanges)
            {
                var questsNode = profileProp.Value?["quests"]?.AsArray();
                if (questsNode == null) continue;

                for (int i = questsNode.Count - 1; i >= 0; i--)
                {
                    var quest = questsNode[i];

                    // Get quest ID (try both "_id" and "qid" formats)
                    var id = quest?["_id"]?.GetValue<string>()
                          ?? quest?["qid"]?.GetValue<string>();
                    if (id == null) continue;

                    // Get quest status (try sptStatus, status, Status)
                    int status = -1;
                    if (quest?["sptStatus"] is JsonNode sptSt)
                        status = sptSt.GetValue<int>();
                    else if (quest?["status"] is JsonNode st)
                        status = st.GetValue<int>();
                    else if (quest?["Status"] is JsonNode stCap)
                        status = stCap.GetValue<int>();

                    if (status == AvailableForStart && !allowedQuestIds.Contains(id))
                    {
                        questsNode.RemoveAt(i);
                        removed++;
                        if (debug)
                            logger.Info($"[MissionControl] Filtered sequel quest {id[..8]}... from completion response");
                    }
                }
            }

            // Inject fresh picks that aren't already in the response, so the new quests appear in
            // the Tasks screen without a reboot.
            var injected = selection.InjectSelected(profileChanges, state, picks);

            if (removed > 0 || injected > 0)
            {
                logger.Info($"[MissionControl] Quest completion: {removed} sequel(s) hidden, {injected} fresh pick(s) shown");
                return ValueTask.FromResult(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            }
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] Error processing quest events: {ex.Message}");
        }

        return ValueTask.FromResult(output ?? string.Empty);
    }
}
