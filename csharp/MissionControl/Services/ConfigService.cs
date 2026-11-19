using MissionControl.Models;
using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;

namespace MissionControl.Services;

[Injectable(InjectionType.Singleton)]
public sealed class ConfigService(ISptLogger<ConfigService> logger)
{
    private readonly object _writeLock = new();
    private static string ConfigPath => Path.Combine(Path.GetDirectoryName(typeof(ConfigService).Assembly.Location)!, "config", "config.jsonc");

    public ModConfig Config => Volatile.Read(ref _config);
    private ModConfig _config = new();
    public HashSet<string> ResolvedTraderWhitelist { get; } = [];

    public void Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                using var defaults = typeof(ConfigService).Assembly.GetManifestResourceStream("MissionControl.DefaultConfig.jsonc")!;
                using var file = new FileStream(ConfigPath, FileMode.CreateNew, FileAccess.Write);
                defaults.CopyTo(file);
            }
            _config = ReadDocument().Settings;
            logger.Info($"[MissionControl] Config loaded: {_config.max_slots} base slots, {_config.max_slots_by_prestige.Count} prestige overrides.");
        }
        catch (Exception ex)
        {
            logger.Warning($"[MissionControl] Could not load config: {ex.Message}");
        }
    }

    public ConfigDocument ReadDocument() => ConfigDocument.Open(ConfigPath);

    public ConfigDocument Save(ConfigDocument original, ModConfig edited)
    {
        if (edited.max_slots < 0 || edited.reroll_cost < 0 ||
            edited.max_slots_by_prestige.Any(p => p.Key <= 0 || p.Value < 0))
            throw new ArgumentException("Slot limits and price must be zero or higher. Prestige levels start at one.");

        lock (_writeLock)
        {
            var current = ReadDocument();
            if (original.Source != current.Source)
                throw new InvalidOperationException("Settings changed since you opened the editor. Reload the saved values before saving.");
            var updated = current.Apply(edited);
            if (updated == current.Source) return current;

            var path = ConfigPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, updated);
                var saved = ConfigDocument.Open(temp);
                File.Move(temp, path, overwrite: true);
                // The trader catalogue and whitelist are initialized at startup. Slot limits are read per request.
                Volatile.Write(ref _config, Config with {
                    count_repeatable_tasks = saved.Settings.count_repeatable_tasks,
                    max_slots = saved.Settings.max_slots,
                    max_slots_by_prestige = new(saved.Settings.max_slots_by_prestige)
                });
                return saved;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
