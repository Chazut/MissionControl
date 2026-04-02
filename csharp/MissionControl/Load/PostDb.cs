using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Services;
using MissionControl.Services;

namespace MissionControl.Load;

[Injectable(TypePriority = OnLoadOrder.PostSptModLoader + 100)]
public sealed class PostDb : IOnLoad
{
    private readonly ISptLogger<PostDb> _logger;
    private readonly ConfigService _configService;
    private readonly ProfileStateStorage _storage;
    private readonly DatabaseService _db;

    public PostDb(ISptLogger<PostDb> logger, ConfigService configService, ProfileStateStorage storage, DatabaseService db)
    {
        _logger = logger;
        _configService = configService;
        _storage = storage;
        _db = db;
    }

    public Task OnLoad()
    {
        // Load vanilla quest IDs from disk
        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        var sptRoot = Path.GetFullPath(Path.Combine(asmDir, "..", "..", ".."));
        var questsPath = Path.Combine(sptRoot, "SPT_Data", "database", "templates", "quests.json");
        VanillaQuestSnapshot.LoadFromFile(questsPath);
        _logger.Info($"[MissionControl] Loaded {VanillaQuestSnapshot.Ids.Count} vanilla quest IDs");

        // Resolve trader whitelist names → IDs
        if (_configService.Config.filter_modded_quests)
            ResolveTraderWhitelist();

        if (_configService.Config.debug_refresh)
        {
            var deleted = _storage.ClearAll();
            if (deleted > 0)
                _logger.Warning($"[MissionControl] debug_refresh: cleared {deleted} saved profile state(s)");
        }

        var cfg = _configService.Config;
        var status = $"[MissionControl] Active — max_slots={cfg.max_slots}, filter_modded={cfg.filter_modded_quests}";
        if (cfg.filter_modded_quests && _configService.ResolvedTraderWhitelist.Count > 0)
            status += $", whitelisted_traders={_configService.ResolvedTraderWhitelist.Count}";
        _logger.Info(status);
        return Task.CompletedTask;
    }

    private void ResolveTraderWhitelist()
    {
        var cfg = _configService.Config;
        if (cfg.trader_whitelist.Count == 0) return;

        var traders = _db.GetTables()?.Traders;
        if (traders == null) return;

        var nameToId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, trader) in traders)
        {
            var nickname = trader.Base?.Nickname;
            if (!string.IsNullOrEmpty(nickname))
                nameToId[nickname] = id.ToString();
        }

        foreach (var name in cfg.trader_whitelist)
        {
            if (nameToId.TryGetValue(name, out var traderId))
            {
                _configService.ResolvedTraderWhitelist.Add(traderId);
                _logger.Info($"[MissionControl] Whitelisted trader: {name} → {traderId}");
            }
            else
            {
                _logger.Warning($"[MissionControl] Trader not found: \"{name}\" — available: {string.Join(", ", nameToId.Keys.Order())}");
            }
        }
    }
}
