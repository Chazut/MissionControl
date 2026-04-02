using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using MissionControl.Services;

namespace MissionControl.Load;

/// <summary>
/// Runs early (after DB load) to create the reroll item and add it to Prapor.
/// Must run before assort caching happens.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.Database + 50)]
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

    public Task OnLoad()
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
