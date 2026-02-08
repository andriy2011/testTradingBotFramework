using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

public class PositionSyncWorker : BackgroundService
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IPositionManager _positionManager;
    private readonly TradingSettings _settings;
    private readonly ILogger<PositionSyncWorker> _logger;

    public PositionSyncWorker(
        IExchangeFactory exchangeFactory,
        IPositionManager positionManager,
        IOptions<TradingSettings> settings,
        ILogger<PositionSyncWorker> logger)
    {
        _exchangeFactory = exchangeFactory;
        _positionManager = positionManager;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PositionSyncWorker starting. Interval: {Interval}s", _settings.PositionSyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.PositionSyncIntervalSeconds), stoppingToken);

            foreach (var exchange in Enum.GetValues<ExchangeName>())
            {
                try
                {
                    var client = _exchangeFactory.GetClient(exchange);
                    var exchangePositions = await client.GetOpenPositionsAsync(stoppingToken);
                    _positionManager.SyncPositions(exchange, exchangePositions);

                    _logger.LogDebug("Synced {Count} positions from {Exchange}", exchangePositions.Count, exchange);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync positions for {Exchange}", exchange);
                }
            }
        }
    }
}
