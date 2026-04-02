using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

/// <summary>
/// Intercepts quest completion events via /client/game/profile/items/moving.
/// Two responsibilities:
/// 1. Remove completed quests from SelectedQuestIds (free the slot)
/// 2. Filter the response to hide newly unlocked quests that aren't in our
///    selected set — prevents sequel quests from appearing immediately
/// </summary>
[Injectable]
public sealed class QuestEventRouter(
    JsonUtil jsonUtil,
    ProfileStateStorage storage,
    ConfigService configService,
    ISptLogger<QuestEventRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ItemEventRouterRequest>(
        "/client/game/profile/items/moving",
        (url, requestData, sessionId, output) =>
            ProcessQuestEvents(sessionId, output, requestData, storage, configService, logger)
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
        ISptLogger<QuestEventRouter> logger)
    {
        if (string.IsNullOrEmpty(output) || requestData?.Data == null)
            return ValueTask.FromResult(output ?? string.Empty);

        // Check if this request contains a quest completion
        var completedQuestIds = new List<string>();
        foreach (var action in requestData.Data)
        {
            if (action is CompleteQuestRequestData completeReq)
                completedQuestIds.Add(completeReq.QuestId.ToString());
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
            storage.Save(sessionIdStr, state);

            // Build the set of quest IDs that should remain visible
            var allowedQuestIds = new HashSet<string>(state.SelectedQuestIds);
            // Also allow the completed quests themselves (their status change must reach the client)
            foreach (var qid in completedQuestIds)
                allowedQuestIds.Add(qid);

            // Filter the response: remove newly unlocked quests from profileChanges
            var node = JsonNode.Parse(output);
            if (node == null)
                return ValueTask.FromResult(output ?? string.Empty);

            var profileChanges = node["data"]?["profileChanges"]?.AsObject()
                ?? node["profileChanges"]?.AsObject();
            if (profileChanges == null)
                return ValueTask.FromResult(output ?? string.Empty);

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

                    // Remove AvailableForStart quests that aren't in our allowed set
                    if (status == AvailableForStart && !allowedQuestIds.Contains(id))
                    {
                        questsNode.RemoveAt(i);
                        removed++;
                        if (debug)
                            logger.Info($"[MissionControl] Filtered sequel quest {id[..8]}... from completion response");
                    }
                }
            }

            if (removed > 0)
                return ValueTask.FromResult(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] Error processing quest events: {ex.Message}");
        }

        return ValueTask.FromResult(output ?? string.Empty);
    }
}
