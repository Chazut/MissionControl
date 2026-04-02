using System.Text.Json;

namespace MissionControl.Services;

/// <summary>
/// Holds the set of vanilla quest IDs, loaded from SPT's quests.json on disk.
/// Used to distinguish modded quests from vanilla ones.
/// </summary>
public static class VanillaQuestSnapshot
{
    public static HashSet<string> Ids { get; } = [];

    public static void LoadFromFile(string questsJsonPath)
    {
        Ids.Clear();

        if (!File.Exists(questsJsonPath))
            return;

        using var stream = File.OpenRead(questsJsonPath);
        using var doc = JsonDocument.Parse(stream);

        foreach (var prop in doc.RootElement.EnumerateObject())
            Ids.Add(prop.Name);
    }

    public static bool IsVanilla(string questId) => Ids.Contains(questId);
}
