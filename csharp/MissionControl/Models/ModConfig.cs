namespace MissionControl.Models;

public sealed record ModConfig
{
    /// <summary>
    /// Maximum number of quests visible at once (in-progress + available combined).
    /// </summary>
    public int max_slots { get; set; } = 3;

    // A tier applies until the next configured tier. An old config has no overrides.
    public SortedDictionary<int, int> max_slots_by_prestige { get; set; } = [];

    public int SlotsFor(int prestige)
    {
        var slots = Math.Max(0, max_slots);
        foreach (var (level, limit) in max_slots_by_prestige)
        {
            if (level > prestige) break;
            if (level > 0 && limit >= 0) slots = limit;
        }
        return slots;
    }

    /// <summary>
    /// When true, modded quests are subject to the slot limit like vanilla quests.
    /// When false, modded quests always pass through (not filtered).
    /// </summary>
    public bool filter_modded_quests { get; set; } = true;

    public bool count_repeatable_tasks { get; set; } = false;

    public List<string> reroll_traders { get; set; } = ["Prapor"];

    /// <summary>
    /// Trader IDs whose quests bypass the slot filter entirely (always visible).
    /// Accepts both vanilla and modded trader IDs.
    /// Example: ["54cb57776803fa99248b456e"] to whitelist Therapist.
    /// </summary>
    public List<string> trader_whitelist { get; set; } = [];

    /// <summary>
    /// Cost in roubles to reroll quest slots at the configured traders.
    /// </summary>
    public int reroll_cost { get; set; } = 50000;


    /// <summary>
    /// Enable verbose logging for debugging.
    /// </summary>
    public bool debug { get; set; } = false;

    /// <summary>
    /// When true, clears all saved slot selections on server start.
    /// Forces a fresh random pick each launch. Only useful during development.
    /// </summary>
    public bool debug_refresh { get; set; } = false;
}
