using MissionControl.Models;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Models.Common;

namespace MissionControl.Services;

/// <summary>
/// Filtering rules shared by QuestListRouter (list fetch) and QuestEventRouter
/// (quest completion): blacklist, whitelist, exemption checks and trader unlocks.
/// </summary>
public static class QuestFilterRules
{
    /// <summary>
    /// Quests that are broken and should never be selected.
    /// </summary>
    public static readonly HashSet<string> QuestBlacklist =
    [
        "67a09761e720611a6a01f288" // Keeper's Word — broken in SPT PvE
    ];

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

    public static HashSet<string> GetUnlockedTraderIds(ProfileHelper profileHelper, MongoId sessionId)
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
}
