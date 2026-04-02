namespace MissionControl.Models;

/// <summary>
/// Persisted state for a single player profile.
/// Tracks which "available" quests are currently selected to fill the visible slots.
/// </summary>
public sealed class ProfileState
{
    /// <summary>
    /// Quest IDs currently selected for display (status AvailableForStart).
    /// These are the randomly picked quests that fill remaining slots after in-progress quests.
    /// </summary>
    public List<string> SelectedQuestIds { get; set; } = [];

    /// <summary>
    /// Quest IDs that were in-progress at the time of a reroll.
    /// These quests remain visible but do NOT consume slots, allowing the player
    /// to have more active quests than max_slots until the exempt ones are completed.
    /// </summary>
    public List<string> ExemptQuestIds { get; set; } = [];
}
