// -----------------------------------------------------------------------
// <file>
//     PositionSyncWorker.cs
//     Background worker that periodically reconciles the local in-memory
//     position book with exchange-reported positions. Runs on a configurable
//     interval (PositionSyncIntervalSeconds, default 30s). For each configured
//     exchange, it fetches the current open positions via the exchange API and
//     calls PositionManager.SyncPositions, which detects mismatches such as
//     quantity differences, positions that exist locally but not on the exchange
//     (phantom positions), and positions on the exchange that are missing locally
//     (orphan positions). Mismatches are logged as warnings for operator review.
//     Sync failures for individual exchanges are caught and logged without
//     affecting other exchanges.
// </file>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Workers;

/// <summary>
/// Background service that reconciles the bot's local position book against
/// the authoritative position data reported by each exchange. This guards
/// against drift caused by missed fill events, manual trades placed directly
/// on the exchange, or partial-fill edge cases.
/// <para>
/// The sync interval is controlled by
/// <see cref="TradingSettings.PositionSyncIntervalSeconds"/> (default 30 seconds).
/// </para>
/// </summary>
public class PositionSyncWorker : BackgroundService
{
    /// <summary>Factory for obtaining exchange-specific API clients.</summary>
    private readonly IExchangeFactory _exchangeFactory;

    /// <summary>Manages the in-memory book of open trading positions.</summary>
    private readonly IPositionManager _positionManager;

    /// <summary>Application-wide trading configuration (sync intervals, thresholds, etc.).</summary>
    private readonly TradingSettings _settings;

    /// <summary>Structured logger scoped to this worker.</summary>
    private readonly ILogger<PositionSyncWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="PositionSyncWorker"/>.
    /// All dependencies are injected by the DI container.
    /// </summary>
    /// <param name="exchangeFactory">Factory to resolve exchange API clients.</param>
    /// <param name="positionManager">Service that tracks open positions.</param>
    /// <param name="settings">
    /// Options wrapper around <see cref="TradingSettings"/>; unwrapped to
    /// <see cref="TradingSettings"/> in the constructor for convenience.
    /// </param>
    /// <param name="logger">Logger instance for structured logging.</param>
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

    /// <summary>
    /// Entry point for the background service. Loops indefinitely on the
    /// configured interval, fetching exchange positions and reconciling them
    /// with the local position book.
    /// </summary>
    /// <param name="stoppingToken">Token that signals graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PositionSyncWorker starting. Interval: {Interval}s", _settings.PositionSyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay first, then sync. This gives other workers (e.g., TradingBotWorker
            // health checks) time to initialize before the first sync attempt.
            await Task.Delay(TimeSpan.FromSeconds(_settings.PositionSyncIntervalSeconds), stoppingToken);

            // Iterate every known exchange independently; a failure on one exchange
            // must not prevent the others from being synced.
            foreach (var exchange in Enum.GetValues<ExchangeName>())
            {
                try
                {
                    // Resolve the exchange-specific client via the factory.
                    var client = _exchangeFactory.GetClient(exchange);

                    // Fetch the authoritative list of open positions from the exchange.
                    var exchangePositions = await client.GetOpenPositionsAsync(stoppingToken);

                    // Reconcile: SyncPositions compares the exchange positions with the
                    // local book and updates/logs any mismatches it detects.
                    _positionManager.SyncPositions(exchange, exchangePositions);

                    _logger.LogDebug("Synced {Count} positions from {Exchange}", exchangePositions.Count, exchange);
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and continue. The exchange may be temporarily
                    // unreachable or not configured for this environment.
                    _logger.LogWarning(ex, "Failed to sync positions for {Exchange}", exchange);
                }
            }
        }
    }
}
