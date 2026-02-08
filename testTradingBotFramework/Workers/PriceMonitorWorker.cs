using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PriceMonitoring;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

public class PriceMonitorWorker : BackgroundService
{
    private readonly IReadOnlyList<KeyValuePair<ExchangeName, IPriceMonitor>> _priceMonitors;
    private readonly IPositionManager _positionManager;
    private readonly ILogger<PriceMonitorWorker> _logger;

    public PriceMonitorWorker(
        IServiceProvider serviceProvider,
        IPositionManager positionManager,
        ILogger<PriceMonitorWorker> logger)
    {
        _priceMonitors = Enum.GetValues<ExchangeName>()
            .Select(e => new KeyValuePair<ExchangeName, IPriceMonitor>(
                e, serviceProvider.GetRequiredKeyedService<IPriceMonitor>(e)))
            .ToList();
        _positionManager = positionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceMonitorWorker starting");

        // Wire up price update events to update positions
        foreach (var (exchange, monitor) in _priceMonitors)
        {
            monitor.OnPriceUpdate += (_, e) =>
            {
                _positionManager.UpdatePositionPrice(e.Exchange, e.Symbol, e.Mid);
            };
        }

        // Periodically check for new positions and subscribe to their price feeds
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var positions = _positionManager.GetOpenPositions();
                foreach (var position in positions)
                {
                    var monitor = _priceMonitors
                        .FirstOrDefault(m => m.Key == position.Exchange).Value;

                    if (monitor is null) continue;

                    if (monitor.GetLatestPrice(position.Symbol) is null)
                    {
                        _logger.LogInformation("Auto-subscribing to price feed for {Exchange}:{Symbol}",
                            position.Exchange, position.Symbol);
                        await monitor.SubscribeAsync(position.Symbol, stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in price monitor subscription loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
