using MissionControl.Models;
using SPTarkov.DI.Annotations;

namespace MissionControl.Services;

/// <summary>
/// Core logic for managing quest slots per profile.
/// Determines which quests should be visible based on the max_slots limit.
///
/// Exempt quests (set by reroll) are in-progress quests that don't consume slots,
/// allowing the player to have more active quests than max_slots temporarily.
/// </summary>
[Injectable]
public sealed class SlotManager
{
    private static readonly Random Rng = new();

    private static class QuestStatus
    {
        public const int AvailableForStart = 1;
        public const int Started = 2;
        public const int AvailableForFinish = 3;
        public const int FailRestartable = 6;
    }

    public HashSet<string> GetVisibleQuestIds(
        List<(string id, int status)> quests,
        ProfileState state,
        int maxSlots)
    {
        var visible = new HashSet<string>();

        // 1. Clean up exempt list: remove quests no longer in-progress (completed/failed)
        var inProgressSet = new HashSet<string>();
        foreach (var (id, status) in quests)
        {
            if (status is QuestStatus.Started or QuestStatus.AvailableForFinish or QuestStatus.FailRestartable)
                inProgressSet.Add(id);
        }
        state.ExemptQuestIds.RemoveAll(id => !inProgressSet.Contains(id));

        // 2. Always show all in-progress quests (exempt or not)
        foreach (var (id, status) in quests)
        {
            if (status is QuestStatus.Started or QuestStatus.AvailableForFinish or QuestStatus.FailRestartable)
                visible.Add(id);
        }

        // 3. Count in-progress quests that are NOT exempt — only these consume slots
        int nonExemptInProgress = 0;
        foreach (var id in inProgressSet)
        {
            if (!state.ExemptQuestIds.Contains(id))
                nonExemptInProgress++;
        }

        int remainingSlots = Math.Max(0, maxSlots - nonExemptInProgress);

        // 4. If no remaining slots, clear selections
        if (remainingSlots <= 0)
        {
            state.SelectedQuestIds.Clear();
            return visible;
        }

        // 5. Get all quests that are AvailableForStart
        var availableIds = new HashSet<string>();
        foreach (var (id, status) in quests)
        {
            if (status == QuestStatus.AvailableForStart)
                availableIds.Add(id);
        }

        // 6. Clean up selections: remove any no longer AvailableForStart
        state.SelectedQuestIds.RemoveAll(id => !availableIds.Contains(id));

        // 7. Fill remaining slots with random picks
        if (state.SelectedQuestIds.Count < remainingSlots)
        {
            var unselected = availableIds
                .Where(id => !state.SelectedQuestIds.Contains(id))
                .ToList();

            Shuffle(unselected);

            int needed = remainingSlots - state.SelectedQuestIds.Count;
            state.SelectedQuestIds.AddRange(unselected.Take(needed));
        }

        // 8. Trim if too many
        if (state.SelectedQuestIds.Count > remainingSlots)
            state.SelectedQuestIds = state.SelectedQuestIds.Take(remainingSlots).ToList();

        // 9. Add selected quests to visible set
        foreach (var id in state.SelectedQuestIds)
            visible.Add(id);

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
