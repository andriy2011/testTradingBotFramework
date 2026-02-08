using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

public class TradingBotWorker : BackgroundService
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IPositionManager _positionManager;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<TradingBotWorker> _logger;

    public TradingBotWorker(
        IExchangeFactory exchangeFactory,
        IPositionManager positionManager,
        IAccountingService accountingService,
        ILogger<TradingBotWorker> logger)
    {
        _exchangeFactory = exchangeFactory;
        _positionManager = positionManager;
        _accountingService = accountingService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradingBotWorker starting. Running startup health checks...");

        await RunHealthChecksAsync(stoppingToken);

        _logger.LogInformation("Startup health checks completed. Entering heartbeat loop.");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Heartbeat: {Time} | Open positions: {Count}",
                DateTimeOffset.UtcNow,
                _positionManager.GetOpenPositions().Count);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RunHealthChecksAsync(CancellationToken ct)
    {
        foreach (var exchange in Enum.GetValues<ExchangeName>())
        {
            try
            {
                var client = _exchangeFactory.GetClient(exchange);
                var balance = await client.GetAccountBalanceAsync(ct);
                _logger.LogInformation("Health check passed for {Exchange}: Balance={Balance} {Currency}",
                    exchange, balance.TotalBalance, balance.Currency);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Health check failed for {Exchange}. Service may not be configured.", exchange);
            }
        }
    }
}
