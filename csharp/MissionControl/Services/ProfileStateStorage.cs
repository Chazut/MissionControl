using System.Text.Json;
using MissionControl.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;

namespace MissionControl.Services;

/// <summary>
/// Persists per-profile slot selections to JSON files on disk.
/// Files are stored in the mod's data/ directory as {sessionId}.json.
/// </summary>
[Injectable]
public sealed class ProfileStateStorage
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _dataDir;
    private readonly ISptLogger<ProfileStateStorage> _logger;

    public ProfileStateStorage(ISptLogger<ProfileStateStorage> logger)
    {
        _logger = logger;
        _dataDir = Path.Combine(GetModDir(), "data");
        Directory.CreateDirectory(_dataDir);
    }

    public ProfileState Load(string sessionId)
    {
        var path = GetPath(sessionId);
        if (!File.Exists(path))
            return new ProfileState();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProfileState>(json) ?? new ProfileState();
        }
        catch (Exception ex)
        {
            _logger.Warning($"[MissionControl] Failed to load profile state for {sessionId}: {ex.Message}");
            return new ProfileState();
        }
    }

    public void Save(string sessionId, ProfileState state)
    {
        try
        {
            var path = GetPath(sessionId);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            _logger.Warning($"[MissionControl] Failed to save profile state for {sessionId}: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes all saved profile states. Returns the number of files deleted.
    /// </summary>
    public int ClearAll()
    {
        var files = Directory.GetFiles(_dataDir, "*.json");
        foreach (var file in files)
        {
            try { File.Delete(file); } catch { }
        }
        return files.Length;
    }

    private string GetPath(string sessionId) => Path.Combine(_dataDir, $"{sessionId}.json");

    private static string GetModDir()
    {
        // The DLL runs from the mod folder (e.g., SPT/user/mods/MissionControl/)
        var assemblyDir = Path.GetDirectoryName(typeof(ProfileStateStorage).Assembly.Location);
        return assemblyDir ?? ".";
    }
}
