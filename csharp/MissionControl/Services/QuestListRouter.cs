using System.Text.Json;
using System.Text.Json.Nodes;
using MissionControl.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Quests;
using SPTarkov.Common.Models.Logging;
using SPTarkov.Server.Core.Utils;

namespace MissionControl.Services;

[Injectable]
public sealed class QuestListRouter(
    JsonUtil jsonUtil,
    SlotManager slotManager,
    ProfileStateStorage storage,
    ConfigService configService,
    ProfileHelper profileHelper,
    ISptLogger<QuestListRouter> logger
) : StaticRouter(jsonUtil, [
    new RouteAction<ListQuestsRequestData>(
        "/client/quest/list",
        (url, requestData, sessionId, output, cancellationToken) =>
            FilterQuestList(sessionId, output, slotManager, storage, configService, profileHelper, logger)
    )
])
{
    /// <summary>
    /// Quests that are broken and should never be selected.
    /// </summary>
    private static readonly HashSet<string> QuestBlacklist =
    [
        "67a09761e720611a6a01f288" // Keeper's Word — broken in SPT PvE
    ];

    /// <summary>
    /// Quests that should always be visible (bypass slot filter).
    /// These are critical progression quests that unlock traders/content.
    /// </summary>
    private static readonly HashSet<string> QuestWhitelist =
    [
        "657315e4a6af4ab4b50f3459", // Saving the Mole (Mechanic) — prerequisite chain to unlock Jaeger
        "5ac23c6186f7741247042bad", // Gunsmith Part 1 (Mechanic) — prerequisite chain to unlock Jaeger
        "5d2495a886f77425cd51e403"  // Introduction (Mechanic) — unlocks Jaeger
    ];

    private static class QuestStatus
    {
        public const int AvailableForStart = 1;
        public const int Started = 2;
        public const int AvailableForFinish = 3;
        public const int FailRestartable = 6;
    }

    private static int GetQuestStatus(JsonElement quest)
    {
        if (quest.TryGetProperty("sptStatus", out var sptStatus))
            return sptStatus.GetInt32();
        if (quest.TryGetProperty("status", out var status))
            return status.GetInt32();
        return -1;
    }

    private static int GetQuestStatusNode(JsonNode? quest)
    {
        if (quest?["sptStatus"] is JsonNode sptStatus)
            return sptStatus.GetValue<int>();
        if (quest?["status"] is JsonNode status)
            return status.GetValue<int>();
        return -1;
    }

    private static HashSet<string> GetUnlockedTraderIds(ProfileHelper profileHelper, MongoId sessionId)
    {
        var unlocked = new HashSet<string>();
        try
        {
            var pmc = profileHelper.GetPmcProfile(sessionId);
            if (pmc?.TradersInfo == null) return unlocked;

            foreach (var (traderId, info) in pmc.TradersInfo)
            {
                if (info.Unlocked == true)
                    unlocked.Add(traderId.ToString());
            }
        }
        catch { }
        return unlocked;
    }

    /// <summary>
    /// Determines if a quest should bypass the slot filter entirely.
    /// A quest is exempt if:
    ///   - Its trader is in the whitelist
    ///   - It's a modded quest and filter_modded_quests is false
    /// </summary>
    private static bool IsExemptFromFilter(string questId, string? traderId, ModConfig config, HashSet<string> resolvedTraderWhitelist)
    {
        // Critical progression quest → always visible
        if (QuestWhitelist.Contains(questId))
            return true;

        // Whitelisted trader → always visible
        if (traderId != null && resolvedTraderWhitelist.Contains(traderId))
            return true;

        // Modded quest and filtering disabled for mods → always visible
        if (!config.filter_modded_quests && !VanillaQuestSnapshot.IsVanilla(questId))
            return true;

        return false;
    }

    private static ValueTask<string> FilterQuestList(
        MongoId sessionId,
        string? output,
        SlotManager slotManager,
        ProfileStateStorage storage,
        ConfigService configService,
        ProfileHelper profileHelper,
        ISptLogger<QuestListRouter> logger)
    {
        if (string.IsNullOrEmpty(output))
            return ValueTask.FromResult(output ?? string.Empty);

        var config = configService.Config;

        try
        {
            using var doc = JsonDocument.Parse(output);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array)
            {
                return ValueTask.FromResult(output);
            }

            var unlockedTraders = GetUnlockedTraderIds(profileHelper, sessionId);

            // Parse all quests from the response
            var allQuests = new List<(string id, int status, string? traderId, bool exempt)>();
            foreach (var quest in dataElement.EnumerateArray())
            {
                var id = quest.TryGetProperty("_id", out var idProp) ? idProp.GetString() : null;
                var status = GetQuestStatus(quest);
                var traderId = quest.TryGetProperty("traderId", out var traderProp) ? traderProp.GetString() : null;

                if (id == null || status < 0) continue;

                // Skip quests known to be broken in SPT PvE mode
                if (QuestBlacklist.Contains(id)) continue;

                var exempt = IsExemptFromFilter(id, traderId, config, configService.ResolvedTraderWhitelist);
                allQuests.Add((id, status, traderId, exempt));
            }

            // Build eligible quest list for SlotManager (excludes exempt quests)
            // Exempt quests don't consume slots and are never filtered
            var eligibleQuests = new List<(string id, int status)>();
            foreach (var (id, status, traderId, exempt) in allQuests)
            {
                if (exempt) continue;

                if (status == QuestStatus.AvailableForStart)
                {
                    if (traderId != null && unlockedTraders.Contains(traderId))
                        eligibleQuests.Add((id, status));
                }
                else
                {
                    eligibleQuests.Add((id, status));
                }
            }

            // Compute visible set from slot manager
            var sessionIdStr = sessionId.ToString();
            var state = storage.Load(sessionIdStr);
            var visibleIds = slotManager.GetVisibleQuestIds(eligibleQuests, state, config.max_slots);
            storage.Save(sessionIdStr, state);

            // Add all exempt quest IDs to visible set
            foreach (var (id, _, _, exempt) in allQuests)
            {
                if (exempt) visibleIds.Add(id);
            }

            // Count filtering
            int totalAvailable = allQuests.Count(q => q.status == QuestStatus.AvailableForStart);
            int visibleAvailable = visibleIds.Count(vid =>
                allQuests.Any(q => q.id == vid && q.status == QuestStatus.AvailableForStart));
            int filteredOut = totalAvailable - visibleAvailable;

            if (config.debug)
            {
                // Log all distinct acceptanceAndFinishingSource values
                var sourceCounts = new Dictionary<string, int>();
                foreach (var quest in dataElement.EnumerateArray())
                {
                    var s = quest.TryGetProperty("acceptanceAndFinishingSource", out var sp) ? sp.GetString() ?? "null" : "missing";
                    sourceCounts[s] = sourceCounts.GetValueOrDefault(s) + 1;
                }
                logger.Info($"[MissionControl][DEBUG] acceptanceAndFinishingSource values: {string.Join(", ", sourceCounts.Select(kv => $"{kv.Key}:{kv.Value}"))}");

                int exemptCount = allQuests.Count(q => q.exempt && q.status == QuestStatus.AvailableForStart);
                logger.Info($"[MissionControl][DEBUG] Total available: {totalAvailable}, exempt: {exemptCount}, eligible: {eligibleQuests.Count(q => q.status == QuestStatus.AvailableForStart)}, filtering out: {filteredOut}");

                // Log selected quests with full details to diagnose client-side hiding
                foreach (var selectedId in state.SelectedQuestIds)
                {
                    // Find the raw quest element in the response for detailed info
                    string questName = "?", side = "?", source = "?";
                    bool secretQuest = false;
                    int minLevel = -1;
                    foreach (var q in dataElement.EnumerateArray())
                    {
                        var qid = q.TryGetProperty("_id", out var p) ? p.GetString() : null;
                        if (qid != selectedId) continue;
                        questName = q.TryGetProperty("QuestName", out p) ? p.GetString() ?? "?" : "?";
                        side = q.TryGetProperty("side", out p) ? p.GetString() ?? "?" : "?";
                        source = q.TryGetProperty("acceptanceAndFinishingSource", out p) ? p.GetString() ?? "?" : "?";
                        secretQuest = q.TryGetProperty("secretQuest", out p) && p.GetBoolean();
                        if (q.TryGetProperty("conditions", out var conds) &&
                            conds.TryGetProperty("AvailableForStart", out var afs) &&
                            afs.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var cond in afs.EnumerateArray())
                            {
                                if (cond.TryGetProperty("conditionType", out var ct) &&
                                    ct.GetString() == "Level" &&
                                    cond.TryGetProperty("value", out var lv))
                                {
                                    minLevel = lv.GetInt32();
                                }
                            }
                        }
                        break;
                    }
                    string gameModes = "?", location = "?";
                    foreach (var q in dataElement.EnumerateArray())
                    {
                        var qid2 = q.TryGetProperty("_id", out var p2) ? p2.GetString() : null;
                        if (qid2 != selectedId) continue;
                        if (q.TryGetProperty("gameModes", out var gm) && gm.ValueKind == JsonValueKind.Array)
                            gameModes = string.Join(",", gm.EnumerateArray().Select(x => x.GetString()));
                        location = q.TryGetProperty("location", out p2) ? p2.GetString() ?? "?" : "?";
                        break;
                    }
                    logger.Info($"[MissionControl][DEBUG]   Slot: {questName} ({selectedId[..12]}...) side={side} source={source} minLvl={minLevel} secret={secretQuest} modes={gameModes} loc={location}");
                }
            }

            if (filteredOut == 0)
                return ValueTask.FromResult(output);

            // Parse as mutable JsonNode and filter
            var node = JsonNode.Parse(output);
            if (node?["data"] is not JsonArray questArray)
                return ValueTask.FromResult(output);

            for (int i = questArray.Count - 1; i >= 0; i--)
            {
                var quest = questArray[i];
                var id = quest?["_id"]?.GetValue<string>();
                var status = GetQuestStatusNode(quest);

                if (id == null) continue;

                if (status == QuestStatus.AvailableForStart && !visibleIds.Contains(id))
                {
                    questArray.RemoveAt(i);
                }
            }

            logger.Info($"[MissionControl] Filtered quest list: showing {questArray.Count}/{dataElement.GetArrayLength()} quests (slots: {config.max_slots})");

            return ValueTask.FromResult(node?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? output);
        }
        catch (Exception ex)
        {
            logger.Error($"[MissionControl] Error filtering quest list: {ex.Message}");
            return ValueTask.FromResult(output ?? string.Empty);
        }
    }
}
