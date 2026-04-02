namespace MissionControl.Models;

public sealed class ModConfig
{
    /// <summary>
    /// Maximum number of quests visible at once (in-progress + available combined).
    /// </summary>
    public int max_slots { get; set; } = 3;

    /// <summary>
    /// When true, modded quests are subject to the slot limit like vanilla quests.
    /// When false, modded quests always pass through (not filtered).
    /// </summary>
    public bool filter_modded_quests { get; set; } = true;

    /// <summary>
    /// Trader IDs whose quests bypass the slot filter entirely (always visible).
    /// Accepts both vanilla and modded trader IDs.
    /// Example: ["54cb57776803fa99248b456e"] to whitelist Therapist.
    /// </summary>
    public List<string> trader_whitelist { get; set; } = [];

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
