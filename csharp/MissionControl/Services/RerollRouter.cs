using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

/// <summary>
/// Intercepts trader purchases. When the reroll item is purchased:
/// 1. the item is consumed server-side AND stripped from the response's new items, so the client
///    never gets a stash item that no longer exists on the server (the old "let the client find it
///    and reload" flow produced a stash item, a desync error on touch, then a reload);
/// 2. the quest slots are cleared, in-progress quests become exempt;
/// 3. the selection runs immediately and the fresh picks ride the same response, so the new quests
///    are in the Tasks screen as soon as the purchase completes.
/// </summary>
[Injectable]
public sealed class RerollRouter(
    JsonUtil jsonUtil,
    ProfileStateStorage storage,
    ProfileHelper profileHelper,
    InventoryHelper inventoryHelper,
    ConfigService configService,
    QuestSelectionService selection,
    ISptLogger<RerollRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ItemEventRouterRequest>(
        "/client/game/profile/items/moving",
        (url, requestData, sessionId, output, cancellationToken) =>
            ProcessPurchase(sessionId, output, storage, profileHelper, inventoryHelper, configService, selection, logger)
    )
])
{
    private static ValueTask<string> ProcessPurchase(
        MongoId sessionId,
        string? output,
        ProfileStateStorage storage,
        ProfileHelper profileHelper,
        InventoryHelper inventoryHelper,
        ConfigService configService,
        QuestSelectionService selection,
        ISptLogger<RerollRouter> logger)
    {
        if (string.IsNullOrEmpty(output))
            return ValueTask.FromResult(output ?? string.Empty);

        try
        {
            var node = JsonNode.Parse(output);
            var profileChanges = node?["data"]?["profileChanges"]?.AsObject()
                ?? node?["profileChanges"]?.AsObject();
            if (profileChanges == null)
                return ValueTask.FromResult(output ?? string.Empty);

            // Find reroll items among the response's new items and strip them out of it
            var rerollItemIds = new List<string>();
            foreach (var profileProp in profileChanges)
            {
                var newItems = profileProp.Value?["items"]?["new"]?.AsArray();
                if (newItems == null) continue;

                for (int i = newItems.Count - 1; i >= 0; i--)
                {
                    var item = newItems[i];
                    if (item?["_tpl"]?.GetValue<string>() != RerollService.RerollItemId) continue;

                    var id = item?["_id"]?.GetValue<string>();
                    if (id != null) rerollItemIds.Add(id);
                    newItems.RemoveAt(i);
                }
            }

            if (rerollItemIds.Count == 0)
                return ValueTask.FromResult(output ?? string.Empty);

            // Consume the item server-side (the client never receives it)
            var pmcData = profileHelper.GetPmcProfile(sessionId);
            foreach (var rerollId in rerollItemIds)
            {
                if (pmcData != null)
                    inventoryHelper.RemoveItem(pmcData, new MongoId(rerollId), sessionId, null);
            }

            // Reroll: clear selections and mark current in-progress quests as exempt
            var sessionIdStr = sessionId.ToString();
            var state = storage.Load(sessionIdStr);
            var previousCount = state.SelectedQuestIds.Count;
            state.SelectedQuestIds.Clear();

            // In-progress quests don't consume slots after a reroll, giving the player breathing room
            state.ExemptQuestIds.Clear();
            if (pmcData?.Quests != null)
            {
                foreach (var quest in pmcData.Quests)
                {
                    if (quest.Status is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish or QuestStatusEnum.FailRestartable)
                        state.ExemptQuestIds.Add(quest.QId.ToString());
                }
            }

            // Pick the new slots right away and ship them with the purchase response
            var picks = selection.Run(sessionId, state);
            storage.Save(sessionIdStr, state);
            var injected = selection.InjectSelected(profileChanges, state, picks);

            logger.Info($"[MissionControl] Reroll purchased — cleared {previousCount} slot(s), exempted {state.ExemptQuestIds.Count} in-progress quest(s), {injected} fresh pick(s) shown");

            return ValueTask.FromResult(node!.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] RerollRouter error: {ex.Message}");
            return ValueTask.FromResult(output ?? string.Empty);
        }
    }
}
