using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Enums;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

/// <summary>
/// Intercepts trader purchases. When the reroll item is purchased,
/// removes it from inventory (both server-side and in the response via del[]),
/// and clears quest slot selections.
/// </summary>
[Injectable]
public sealed class RerollRouter(
    JsonUtil jsonUtil,
    ProfileStateStorage storage,
    ProfileHelper profileHelper,
    InventoryHelper inventoryHelper,
    ConfigService configService,
    ISptLogger<RerollRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ItemEventRouterRequest>(
        "/client/game/profile/items/moving",
        (url, requestData, sessionId, output) =>
            ProcessPurchase(sessionId, output, requestData, storage, profileHelper, inventoryHelper, configService, logger)
    )
])
{
    private static ValueTask<string> ProcessPurchase(
        MongoId sessionId,
        string? output,
        ItemEventRouterRequest? requestData,
        ProfileStateStorage storage,
        ProfileHelper profileHelper,
        InventoryHelper inventoryHelper,
        ConfigService configService,
        ISptLogger<RerollRouter> logger)
    {
        if (string.IsNullOrEmpty(output) || requestData?.Data == null)
            return ValueTask.FromResult(output ?? string.Empty);

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            var profileChanges = root.TryGetProperty("data", out var data)
                && data.TryGetProperty("profileChanges", out var pc) ? pc
                : root.TryGetProperty("profileChanges", out pc) ? pc
                : default;

            if (profileChanges.ValueKind != JsonValueKind.Object)
                return ValueTask.FromResult(output ?? string.Empty);

            // Find reroll items in new items
            var rerollItemIds = new List<string>();
            foreach (var profileProp in profileChanges.EnumerateObject())
            {
                if (!profileProp.Value.TryGetProperty("items", out var items)) continue;
                if (!items.TryGetProperty("new", out var newItems)) continue;
                if (newItems.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in newItems.EnumerateArray())
                {
                    var tpl = item.TryGetProperty("_tpl", out var tplProp) ? tplProp.GetString() : null;
                    if (tpl != RerollService.RerollItemId) continue;

                    var id = item.TryGetProperty("_id", out var idProp) ? idProp.GetString() : null;
                    if (id != null) rerollItemIds.Add(id);
                }
            }

            if (rerollItemIds.Count == 0)
                return ValueTask.FromResult(output ?? string.Empty);

            // Remove from server inventory only — leave in response so client receives it.
            // The client polling will detect the item and trigger a profile reload,
            // which will make the item disappear (already removed server-side).
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

            // Find all in-progress quests from the player's profile and mark them exempt
            // These quests won't consume slots, giving the player breathing room
            state.ExemptQuestIds.Clear();
            if (pmcData?.Quests != null)
            {
                foreach (var quest in pmcData.Quests)
                {
                    if (quest.Status is QuestStatusEnum.Started or QuestStatusEnum.AvailableForFinish or QuestStatusEnum.FailRestartable)
                    {
                        state.ExemptQuestIds.Add(quest.QId.ToString());
                    }
                }
            }

            storage.Save(sessionIdStr, state);

            logger.Info($"[MissionControl] Reroll purchased — cleared {previousCount} slots, exempted {state.ExemptQuestIds.Count} in-progress quests");

            return ValueTask.FromResult(output ?? string.Empty);
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] RerollRouter error: {ex.Message}");
            return ValueTask.FromResult(output ?? string.Empty);
        }
    }
}
