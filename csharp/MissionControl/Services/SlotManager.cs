using MissionControl.Models;
using SPTarkov.DI.Annotations;

namespace MissionControl.Services;

/// <summary>
/// Core logic for managing quest slots per profile.
/// Determines which quests should be visible based on the max_slots limit.
/// </summary>
[Injectable]
public sealed class SlotManager
{
    private static readonly Random Rng = new();

    /// <summary>
    /// EQuestStatus values from the SPT/EFT quest system.
    /// </summary>
    private static class QuestStatus
    {
        public const int AvailableForStart = 1;
        public const int Started = 2;
        public const int AvailableForFinish = 3;
        public const int FailRestartable = 6;
    }

    /// <summary>
    /// Given the full quest list and the persisted profile state,
    /// returns the set of quest IDs that should remain visible.
    /// Also updates the profile state with current selections.
    /// </summary>
    /// <param name="quests">All quests with their id and status from the server response.</param>
    /// <param name="state">Persisted profile state (will be mutated with new selections).</param>
    /// <param name="maxSlots">Maximum total visible quests.</param>
    /// <returns>Set of quest IDs to keep in the response.</returns>
    public HashSet<string> GetVisibleQuestIds(
        List<(string id, int status)> quests,
        ProfileState state,
        int maxSlots)
    {
        var visible = new HashSet<string>();

        // 1. Always show in-progress quests (Started, AvailableForFinish)
        //    and failed-restartable quests (they stay in their slot)
        var inProgressIds = new List<string>();
        foreach (var (id, status) in quests)
        {
            if (status is QuestStatus.Started or QuestStatus.AvailableForFinish or QuestStatus.FailRestartable)
            {
                visible.Add(id);
                inProgressIds.Add(id);
            }
        }

        int inProgressCount = inProgressIds.Count;
        int remainingSlots = Math.Max(0, maxSlots - inProgressCount);

        // 2. If no remaining slots, clear selections and return only in-progress
        if (remainingSlots <= 0)
        {
            state.SelectedQuestIds.Clear();
            return visible;
        }

        // 3. Get all quests that are AvailableForStart
        var availableIds = new HashSet<string>();
        foreach (var (id, status) in quests)
        {
            if (status == QuestStatus.AvailableForStart)
                availableIds.Add(id);
        }

        // 4. Clean up persisted selections: remove any that are no longer AvailableForStart
        state.SelectedQuestIds.RemoveAll(id => !availableIds.Contains(id));

        // 5. If we need more selections, pick randomly from unselected available quests
        if (state.SelectedQuestIds.Count < remainingSlots)
        {
            var unselected = availableIds
                .Where(id => !state.SelectedQuestIds.Contains(id))
                .ToList();

            Shuffle(unselected);

            int needed = remainingSlots - state.SelectedQuestIds.Count;
            state.SelectedQuestIds.AddRange(unselected.Take(needed));
        }

        // 6. If we have too many selections (e.g., maxSlots was reduced), trim
        if (state.SelectedQuestIds.Count > remainingSlots)
        {
            state.SelectedQuestIds = state.SelectedQuestIds.Take(remainingSlots).ToList();
        }

        // 7. Add selected quests to visible set
        foreach (var id in state.SelectedQuestIds)
        {
            visible.Add(id);
        }

        return visible;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
