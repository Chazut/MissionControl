using System.Text.Json;
using System.Text.Json.Nodes;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.ItemEvent;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers.Ws;
using SPTarkov.Server.Core.Models.Eft.Ws;
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
    SptWebSocketConnectionHandler wsHandler,
    ConfigService configService,
    ISptLogger<RerollRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ItemEventRouterRequest>(
        "/client/game/profile/items/moving",
        (url, requestData, sessionId, output) =>
            ProcessPurchase(sessionId, output, requestData, storage, profileHelper, inventoryHelper, wsHandler, configService, logger)
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
        SptWebSocketConnectionHandler wsHandler,
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

            // Modify the response:
            // 1. Remove reroll items from "new"
            // 2. Add them to "del" so the client knows to remove them
            var node = JsonNode.Parse(output);
            var pcNode = node?["data"]?["profileChanges"]?.AsObject()
                ?? node?["profileChanges"]?.AsObject();

            var pmcData = profileHelper.GetPmcProfile(sessionId);

            foreach (var rerollId in rerollItemIds)
            {
                // Remove from server inventory
                if (pmcData != null)
                    inventoryHelper.RemoveItem(pmcData, new MongoId(rerollId), sessionId, null);

                if (pcNode == null) continue;

                foreach (var prop in pcNode)
                {
                    var itemsNode = prop.Value?["items"];
                    if (itemsNode == null) continue;

                    // Remove from "new"
                    var newArr = itemsNode["new"]?.AsArray();
                    if (newArr != null)
                    {
                        for (int i = newArr.Count - 1; i >= 0; i--)
                        {
                            if (newArr[i]?["_id"]?.GetValue<string>() == rerollId)
                                newArr.RemoveAt(i);
                        }
                    }

                    // Add to "del" — tells the client to delete this item
                    var delArr = itemsNode["del"]?.AsArray();
                    if (delArr == null)
                    {
                        delArr = new JsonArray();
                        itemsNode.AsObject()["del"] = delArr;
                    }
                    delArr.Add(new JsonObject { ["_id"] = rerollId, ["_tpl"] = RerollService.RerollItemId });
                }
            }

            // Clear quest slot selections
            var sessionIdStr = sessionId.ToString();
            var state = storage.Load(sessionIdStr);
            var previousCount = state.SelectedQuestIds.Count;
            state.SelectedQuestIds.Clear();
            storage.Save(sessionIdStr, state);

            logger.Info($"[MissionControl] Reroll purchased — cleared {previousCount} quest slots, forcing re-login");

            // Force client to re-login so it re-requests the quest list with new slots
            Task.Run(async () =>
            {
                await Task.Delay(1500); // let the purchase response reach the client first
                wsHandler.SendMessage(sessionId, new WsNotificationEvent
                {
                    EventType = NotificationEventType.ForceLogout,
                    EventIdentifier = new MongoId(Guid.NewGuid().ToString("N")[..24])
                });
                logger.Info("[MissionControl] ForceLogout sent");
            });

            return ValueTask.FromResult(node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? output);
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] RerollRouter error: {ex.Message}");
            return ValueTask.FromResult(output ?? string.Empty);
        }
    }
}
