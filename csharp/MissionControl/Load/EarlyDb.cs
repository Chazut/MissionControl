using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using MissionControl.Services;

namespace MissionControl.Load;

/// <summary>
/// Register the uplink after custom traders have registered and before SPT builds its handbook and trader caches.
/// </summary>
[Injectable(TypePriority = OnLoadOrder.HandbookCallbacks - 1)]
public sealed class EarlyDb : IOnLoad
{
    private readonly ConfigService _configService;
    private readonly RerollService _rerollService;

    public EarlyDb(ConfigService configService, RerollService rerollService)
    {
        _configService = configService;
        _rerollService = rerollService;
    }

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        _configService.Load();
        if (_rerollService.CreateRerollItem())
            _rerollService.StockVendors(_configService.Config.reroll_traders, _configService.Config.reroll_cost);

        return Task.CompletedTask;
    }
}
