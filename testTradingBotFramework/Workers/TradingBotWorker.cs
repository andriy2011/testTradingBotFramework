// -----------------------------------------------------------------------
// <file>
//     TradingBotWorker.cs
//     Primary background worker for the trading bot framework.
//     On startup, performs health checks against all configured exchanges by
//     verifying API connectivity (fetches account balance from each exchange).
//     After health checks complete, enters a 30-second heartbeat loop that logs
//     the current UTC timestamp and the number of open positions. This heartbeat
//     serves as a liveness indicator for monitoring and alerting systems.
//     Health check failures are non-fatal -- the worker warns but continues,
//     since not all exchanges may be configured in every deployment environment.
// </file>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

/// <summary>
/// The primary background service for the trading bot. Inherits from
/// <see cref="BackgroundService"/> so it is automatically started and stopped
/// by the .NET Generic Host lifetime.
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item>Run startup health checks against every registered exchange.</item>
///   <item>Emit periodic heartbeat logs with open position count.</item>
/// </list>
/// </para>
/// </summary>
public class TradingBotWorker : BackgroundService
{
    /// <summary>Factory for obtaining exchange-specific API clients (Binance, OANDA, etc.).</summary>
    private readonly IExchangeFactory _exchangeFactory;

    /// <summary>Manages the in-memory book of open trading positions.</summary>
    private readonly IPositionManager _positionManager;

    /// <summary>Tracks account balances and P&amp;L across exchanges.</summary>
    private readonly IAccountingService _accountingService;

    /// <summary>Structured logger scoped to this worker.</summary>
    private readonly ILogger<TradingBotWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="TradingBotWorker"/>.
    /// All dependencies are injected by the DI container.
    /// </summary>
    /// <param name="exchangeFactory">Factory to resolve exchange API clients.</param>
    /// <param name="positionManager">Service that tracks open positions.</param>
    /// <param name="accountingService">Service that tracks balances and P&amp;L.</param>
    /// <param name="logger">Logger instance for structured logging.</param>
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

    /// <summary>
    /// Entry point for the background service. Called once by the host when the
    /// application starts. Runs health checks, then enters a heartbeat loop that
    /// continues until the host signals cancellation (e.g., SIGTERM or Ctrl+C).
    /// </summary>
    /// <param name="stoppingToken">Token that signals graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TradingBotWorker starting. Running startup health checks...");

        // Perform one-time startup health checks before entering the main loop.
        await RunHealthChecksAsync(stoppingToken);

        _logger.LogInformation("Startup health checks completed. Entering heartbeat loop.");

        // Heartbeat loop: log a pulse every 30 seconds so operators can confirm
        // the bot is alive and see how many positions are currently open.
        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("Heartbeat: {Time} | Open positions: {Count}",
                DateTimeOffset.UtcNow,
                _positionManager.GetOpenPositions().Count);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    /// <summary>
    /// Iterates over every <see cref="ExchangeName"/> enum value and attempts to
    /// fetch the account balance from each exchange. A successful balance fetch
    /// confirms that API credentials are valid and the network path is open.
    /// Failures are logged as warnings (not errors) because some exchanges may
    /// intentionally be left unconfigured in certain environments.
    /// </summary>
    /// <param name="ct">Cancellation token to abort health checks on shutdown.</param>
    private async Task RunHealthChecksAsync(CancellationToken ct)
    {
        // Iterate every known exchange; each check is independent so a failure
        // in one does not prevent the others from being tested.
        foreach (var exchange in Enum.GetValues<ExchangeName>())
        {
            try
            {
                // Resolve the exchange-specific client via the factory.
                var client = _exchangeFactory.GetClient(exchange);

                // Attempt to fetch the account balance as a lightweight connectivity test.
                var balance = await client.GetAccountBalanceAsync(ct);
                _logger.LogInformation("Health check passed for {Exchange}: Balance={Balance} {Currency}",
                    exchange, balance.TotalBalance, balance.Currency);
            }
            catch (Exception ex)
            {
                // Non-fatal: the exchange may simply not be configured for this deployment.
                _logger.LogWarning(ex, "Health check failed for {Exchange}. Service may not be configured.", exchange);
            }
        }
    }
}
