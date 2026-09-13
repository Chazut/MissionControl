using System.Text.Json;
using System.Text.RegularExpressions;
using MissionControl.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Common.Models.Logging;

namespace MissionControl.Services;

/// <summary>
/// Loads and holds the mod configuration from config/config.jsonc.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed partial class ConfigService
{
    private readonly ISptLogger<ConfigService> _logger;

    public ModConfig Config { get; private set; } = new();

    /// <summary>
    /// Trader IDs resolved from the trader_whitelist names.
    /// Populated by PostDb after the database is loaded.
    /// </summary>
    public HashSet<string> ResolvedTraderWhitelist { get; } = [];

    public ConfigService(ISptLogger<ConfigService> logger)
    {
        _logger = logger;
    }

    public void Load()
    {
        var configDir = GetConfigDir();
        var configPath = Path.Combine(configDir, "config.jsonc");

        if (!File.Exists(configPath))
        {
            _logger.Warning($"[MissionControl] Config not found at {configPath}, using defaults (max_slots={Config.max_slots})");
            return;
        }

        try
        {
            var raw = File.ReadAllText(configPath);
            var json = StripJsoncComments(raw);
            Config = JsonSerializer.Deserialize<ModConfig>(json) ?? new ModConfig();
            _logger.Info($"[MissionControl] Config loaded: max_slots={Config.max_slots}");
        }
        catch (Exception ex)
        {
            _logger.Warning($"[MissionControl] Failed to load config: {ex.Message}, using defaults");
        }
    }

    private static string GetConfigDir()
    {
        var assemblyDir = Path.GetDirectoryName(typeof(ConfigService).Assembly.Location);
        return Path.Combine(assemblyDir ?? ".", "config");
    }

    /// <summary>
    /// Strips single-line (//) and multi-line (/* */) comments from JSONC.
    /// </summary>
    private static string StripJsoncComments(string input)
    {
        // Remove multi-line comments
        var result = MultiLineCommentRegex().Replace(input, string.Empty);
        // Remove single-line comments (not inside strings)
        result = SingleLineCommentRegex().Replace(result, string.Empty);
        return result;
    }

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex MultiLineCommentRegex();

    [GeneratedRegex(@"//.*?$", RegexOptions.Multiline)]
    private static partial Regex SingleLineCommentRegex();
}
