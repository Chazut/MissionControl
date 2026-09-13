using System.Text.Json.Nodes;
using MissionControl.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Quest;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

/// <summary>
/// Runs the slot selection on the live quest pool (outside of /client/quest/list) and pushes the
/// picks into an item-event response, so the Tasks screen updates right away instead of waiting for
/// the next quest list fetch (reboot or raid end). Shared by the quest-completion and reroll routers.
/// </summary>
[Injectable]
public sealed class QuestSelectionService(
    QuestHelper questHelper,
    ProfileHelper profileHelper,
    SlotManager slotManager,
    ConfigService configService,
    JsonUtil jsonUtil,
    ISptLogger<QuestSelectionService> logger)
{
    private const int AvailableForStart = 1;

    public sealed class Selection
    {
        public HashSet<string> VisibleIds { get; set; } = [];
        public HashSet<string> ExemptIds { get; } = [];
        public Dictionary<string, Quest> AvailableById { get; } = new();
    }

    /// <summary>
    /// Same rules as the quest list filter (blacklist, whitelist, unlocked traders, exemptions),
    /// applied to the quests the server would hand the client right now. Mutates the state's
    /// selections; the caller saves it.
    /// </summary>
    public Selection Run(MongoId sessionId, ProfileState state)
    {
        var config = configService.Config;
        var selection = new Selection();
        var clientQuests = questHelper.GetClientQuests(sessionId);
        var unlockedTraders = QuestFilterRules.GetUnlockedTraderIds(profileHelper, sessionId);

        var eligibleQuests = new List<(string id, int status)>();
        foreach (var quest in clientQuests)
        {
            var id = quest.Id.ToString();
            var status = (int?)quest.SptStatus ?? -1;
            var traderId = quest.TraderId.ToString();

            if (QuestFilterRules.QuestBlacklist.Contains(id)) continue;

            if (status == AvailableForStart)
                selection.AvailableById[id] = quest;

            if (QuestFilterRules.IsExemptFromFilter(id, traderId, config, configService.ResolvedTraderWhitelist))
            {
                selection.ExemptIds.Add(id);
                continue;
            }

            if (status == AvailableForStart)
            {
                if (unlockedTraders.Contains(traderId))
                    eligibleQuests.Add((id, status));
            }
            else
            {
                eligibleQuests.Add((id, status));
            }
        }

        selection.VisibleIds = slotManager.GetVisibleQuestIds(eligibleQuests, state, config.max_slots);
        return selection;
    }

    /// <summary>
    /// Adds every selected quest missing from the response's profileChanges quests arrays (created
    /// when absent), in the exact shape vanilla ships sequel quests on completion. Returns the count.
    /// </summary>
    public int InjectSelected(JsonObject profileChanges, ProfileState state, Selection selection)
    {
        var injected = 0;
        foreach (var profileProp in profileChanges)
        {
            if (profileProp.Value is not JsonObject profileNode) continue;

            if (profileNode["quests"] is not JsonArray questsNode)
            {
                questsNode = new JsonArray();
                profileNode["quests"] = questsNode;
            }

            var presentIds = new HashSet<string>();
            foreach (var quest in questsNode)
            {
                var id = quest?["_id"]?.GetValue<string>() ?? quest?["qid"]?.GetValue<string>();
                if (id != null) presentIds.Add(id);
            }

            foreach (var selectedId in state.SelectedQuestIds)
            {
                if (presentIds.Contains(selectedId)) continue;
                if (!selection.AvailableById.TryGetValue(selectedId, out var quest)) continue;

                var questJson = jsonUtil.Serialize(quest);
                if (questJson == null) continue;

                questsNode.Add(JsonNode.Parse(questJson));
                injected++;
                if (configService.Config.debug)
                    logger.Info($"[MissionControl] Injected fresh pick {selectedId[..8]}... into the response");
            }
        }
        return injected;
    }
}
