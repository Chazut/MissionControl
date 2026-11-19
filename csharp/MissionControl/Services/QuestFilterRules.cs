using System.Text.Json;
using System.Text.Json.Nodes;
using MissionControl.Models;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;

namespace MissionControl.Services;

/// <summary>
/// Filtering rules shared by QuestListRouter (list fetch) and QuestEventRouter
/// (quest completion): blacklist, whitelist, exemption checks and trader unlocks.
/// </summary>
public static class QuestFilterRules
{
    // Story chapters and their hidden helper quests must reach the client with their original
    // conditions. VisitAPI decides when to start them, including stages owned by locked traders.
    public static bool IsStoryOrHidden(JsonElement quest) =>
        HasTrueFlag(quest, "isStoryQuest") || HasTrueFlag(quest, "notDisplayedQuest");

    private static bool HasTrueFlag(JsonElement quest, string key) =>
        quest.TryGetProperty(key, out var flag) && flag.ValueKind == JsonValueKind.True;

    public static bool IsStoryOrHidden(JsonObject quest) =>
        quest["isStoryQuest"]?.GetValueKind() == JsonValueKind.True ||
        quest["notDisplayedQuest"]?.GetValueKind() == JsonValueKind.True;

    // SPT 4.1.6 keeps these fields in JsonExtensionData. Earlier versions can omit the bag.
    private static readonly Func<Quest, Dictionary<string, object?>?>? ReadExtras =
        typeof(Quest).GetProperty("ExtensionData")?.GetMethod?.CreateDelegate<Func<Quest, Dictionary<string, object?>?>>();

    public static bool IsStoryOrHidden(Quest quest)
    {
        var extras = ReadExtras?.Invoke(quest);
        return extras != null && (HasTrueFlag(extras, "isStoryQuest") || HasTrueFlag(extras, "notDisplayedQuest"));
    }

    private static bool HasTrueFlag(Dictionary<string, object?> extras, string key) =>
        extras.TryGetValue(key, out var flag) && flag is true or JsonElement { ValueKind: JsonValueKind.True };

    /// <summary>
    /// Quests that are broken and should never be selected.
    /// </summary>
    public static readonly HashSet<string> QuestBlacklist =
    [
        "67a09761e720611a6a01f288" // Keeper's Word — broken in SPT PvE
    ];

    /// <summary>
    /// Traders whose quests are never offered, whatever the config says. The BTR Driver's 15 quests (Hot Wheels,
    /// Shipping Delay, Battery Change, Protect the Sky...) all hinge on the BTR taxi and its paid services, which
    /// is not a quest line anyone wants filling their limited slots.
    /// </summary>
    public static readonly HashSet<string> TraderBlacklist =
    [
        "656f0f98d80a697f855d34b1" // BTR Driver
    ];

    /// <summary>True when the quest itself or its trader is blacklisted: hidden from the list, never selected.</summary>
    public static bool IsBlacklisted(string questId, string? traderId)
        => QuestBlacklist.Contains(questId) || (traderId != null && TraderBlacklist.Contains(traderId));

    /// <summary>
    /// Quests that should always be visible (bypass slot filter).
    /// These are critical progression quests that unlock traders/content.
    /// </summary>
    public static readonly HashSet<string> QuestWhitelist =
    [
        "657315e4a6af4ab4b50f3459", // Saving the Mole (Mechanic) — prerequisite chain to unlock Jaeger
        "5ac23c6186f7741247042bad", // Gunsmith Part 1 (Mechanic) — prerequisite chain to unlock Jaeger
        "5d2495a886f77425cd51e403"  // Introduction (Mechanic) — unlocks Jaeger
    ];

    /// <summary>
    /// Determines if a quest should bypass the slot filter entirely.
    /// A quest is exempt if:
    ///   - It's in the whitelist
    ///   - Its trader is in the whitelist
    ///   - It's a modded quest and filter_modded_quests is false
    /// </summary>
    public static bool IsExemptFromFilter(string questId, string? traderId, ModConfig config, HashSet<string> resolvedTraderWhitelist)
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

    public static HashSet<string> GetUnlockedTraderIds(ProfileHelper profileHelper, MongoId sessionId, out int prestige)
    {
        var unlocked = new HashSet<string>();
        prestige = 0;
        try
        {
            var pmc = profileHelper.GetPmcProfile(sessionId);
            prestige = pmc?.Info?.PrestigeLevel ?? 0;
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
}
