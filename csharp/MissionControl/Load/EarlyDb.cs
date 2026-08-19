using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Common.Models.Logging;
using MissionControl.Services;

namespace MissionControl.Load;

/// <summary>
/// Runs early to create the reroll item and add it to Prapor.
/// Must run before trader load/assort caching happens (TraderCallbacks).
/// 4.1: the database is fully imported before any IOnLoad stage runs, so
/// Preload is the earliest safe equivalent of the old Database stage.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Preload + 50)]
public sealed class EarlyDb : IOnLoad
{
    private readonly ISptLogger<EarlyDb> _logger;
    private readonly ConfigService _configService;
    private readonly RerollService _rerollService;

    public EarlyDb(ISptLogger<EarlyDb> logger, ConfigService configService, RerollService rerollService)
    {
        _logger = logger;
        _configService = configService;
        _rerollService = rerollService;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        // Load config early so reroll_cost is available
        _configService.Load();

        if (_rerollService.CreateRerollItem())
        {
            if (_rerollService.AddToPrapor(_configService.Config.reroll_cost))
                _logger.Info($"[MissionControl] Reroll item added to Prapor ({_configService.Config.reroll_cost}₽)");
            else
                _logger.Warning("[MissionControl] Failed to add reroll item to Prapor's assort");
        }

        return Task.CompletedTask;
    }
}
