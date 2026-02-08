// -----------------------------------------------------------------------
// <file>
//     PriceMonitorWorker.cs
//     Background worker that manages real-time price feeds for all open positions.
//     Wires OnPriceUpdate events from exchange-specific IPriceMonitor implementations
//     to the PositionManager so that unrealized P&L is continuously updated with
//     the latest mid-price. Every 5 seconds, the worker scans open positions and
//     auto-subscribes to price feeds for any symbol that does not yet have an
//     active subscription. Uses .NET 8 keyed DI to resolve exchange-specific
//     IPriceMonitor implementations (e.g., BinancePriceMonitor uses WebSocket,
//     OandaPriceMonitor uses SSE streaming).
// </file>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PriceMonitoring;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

/// <summary>
/// Background service responsible for maintaining live price feeds for every
/// symbol that has an open position. Each exchange has its own
/// <see cref="IPriceMonitor"/> implementation resolved via keyed dependency
/// injection. When a price update arrives, it is forwarded to the
/// <see cref="IPositionManager"/> to keep unrealized P&amp;L up to date.
/// <para>
/// The worker operates in two phases:
/// <list type="number">
///   <item>Registers event handlers on all exchange price monitors (one-time setup).</item>
///   <item>Enters a 5-second polling loop that detects newly opened positions and
///         subscribes to their price feeds automatically.</item>
/// </list>
/// </para>
/// </summary>
public class PriceMonitorWorker : BackgroundService
{
    /// <summary>
    /// Pre-resolved list of exchange-to-price-monitor mappings. Built once in the
    /// constructor so the worker avoids repeated service lookups at runtime.
    /// Each entry pairs an <see cref="ExchangeName"/> with its keyed
    /// <see cref="IPriceMonitor"/> implementation.
    /// </summary>
    private readonly IReadOnlyList<KeyValuePair<ExchangeName, IPriceMonitor>> _priceMonitors;

    /// <summary>Manages the in-memory book of open trading positions.</summary>
    private readonly IPositionManager _positionManager;

    /// <summary>Structured logger scoped to this worker.</summary>
    private readonly ILogger<PriceMonitorWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PriceMonitorWorker"/>.
    /// Eagerly resolves all keyed <see cref="IPriceMonitor"/> services from the
    /// DI container so they are ready when <see cref="ExecuteAsync"/> runs.
    /// </summary>
    /// <param name="serviceProvider">
    /// Root service provider, used to resolve keyed <see cref="IPriceMonitor"/>
    /// instances (one per <see cref="ExchangeName"/>).
    /// </param>
    /// <param name="positionManager">Service that tracks open positions.</param>
    /// <param name="logger">Logger instance for structured logging.</param>
    public PriceMonitorWorker(
        IServiceProvider serviceProvider,
        IPositionManager positionManager,
        ILogger<PriceMonitorWorker> logger)
    {
        // Resolve the keyed IPriceMonitor for every known exchange at construction
        // time. This produces a flat list of (ExchangeName, IPriceMonitor) pairs
        // that is iterated in the main loop.
        _priceMonitors = Enum.GetValues<ExchangeName>()
            .Select(e => new KeyValuePair<ExchangeName, IPriceMonitor>(
                e, serviceProvider.GetRequiredKeyedService<IPriceMonitor>(e)))
            .ToList();
        _positionManager = positionManager;
        _logger = logger;
    }

    /// <summary>
    /// Entry point for the background service. Registers price-update event
    /// handlers once, then polls every 5 seconds to auto-subscribe to price
    /// feeds for any newly opened positions.
    /// </summary>
    /// <param name="stoppingToken">Token that signals graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PriceMonitorWorker starting");

        // --- Phase 1: One-time event wiring ---
        // Subscribe to OnPriceUpdate for every exchange monitor. When a new
        // price tick arrives, forward the mid-price to the position manager
        // so it can recalculate unrealized P&L for the affected position.
        foreach (var (exchange, monitor) in _priceMonitors)
        {
            monitor.OnPriceUpdate += (_, e) =>
            {
                _positionManager.UpdatePositionPrice(e.Exchange, e.Symbol, e.Mid);
            };
        }

        // --- Phase 2: Auto-subscription polling loop ---
        // Every 5 seconds, scan all open positions and ensure each one has an
        // active price feed subscription. New positions (opened by signal
        // workers or manual trades) are detected and subscribed automatically.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var positions = _positionManager.GetOpenPositions();
                foreach (var position in positions)
                {
                    // Find the price monitor for this position's exchange.
                    var monitor = _priceMonitors
                        .FirstOrDefault(m => m.Key == position.Exchange).Value;

                    // Skip if no monitor is registered for this exchange.
                    if (monitor is null) continue;

                    // If the monitor has no cached price for this symbol, it means
                    // we have not yet subscribed to its real-time feed.
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
                // Log and continue: transient errors (network blips, brief exchange
                // outages) should not kill the subscription loop permanently.
                _logger.LogError(ex, "Error in price monitor subscription loop");
            }

            // Wait 5 seconds before the next scan for unsubscribed positions.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }
}
