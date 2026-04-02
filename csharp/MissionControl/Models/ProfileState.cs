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
}
