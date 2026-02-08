// -----------------------------------------------------------------------
// <file>
//     AccountSyncWorker.cs
//     Background worker that periodically fetches account balances from each
//     configured exchange and runs P&L reconciliation. Operates on a configurable
//     interval (AccountSyncIntervalSeconds, default 60s). For each exchange the
//     worker: (1) fetches the latest account balance via the exchange API,
//     (2) updates the AccountingService with the fresh balance data, and
//     (3) compares the locally computed P&L against the exchange-reported P&L.
//     If the divergence exceeds the configured ReconciliationThreshold, a
//     RECONCILIATION WARNING is logged so operators can investigate potential
//     discrepancies (missed fills, fee miscalculations, external deposits/withdrawals).
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
using testTradingBotFramework.Services.Accounting;

namespace testTradingBotFramework.Workers;

/// <summary>
/// Background service that keeps account balance snapshots up to date and
/// performs periodic P&amp;L reconciliation between the bot's internal ledger
/// and the exchange-reported figures. This is a critical safety net: if
/// local accounting diverges from exchange reality (e.g., due to missed
/// fill notifications, fee changes, or manual trades), the reconciliation
/// warning alerts operators before the discrepancy compounds.
/// <para>
/// The sync interval is controlled by
/// <see cref="TradingSettings.AccountSyncIntervalSeconds"/> (default 60 seconds).
/// </para>
/// </summary>
public class AccountSyncWorker : BackgroundService
{
    /// <summary>Factory for obtaining exchange-specific API clients.</summary>
    private readonly IExchangeFactory _exchangeFactory;

    /// <summary>Service that tracks balances, realized P&amp;L, and performs reconciliation.</summary>
    private readonly IAccountingService _accountingService;

    /// <summary>Application-wide trading configuration (sync intervals, thresholds, etc.).</summary>
    private readonly TradingSettings _settings;

    /// <summary>Structured logger scoped to this worker.</summary>
    private readonly ILogger<AccountSyncWorker> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="AccountSyncWorker"/>.
    /// All dependencies are injected by the DI container.
    /// </summary>
    /// <param name="exchangeFactory">Factory to resolve exchange API clients.</param>
    /// <param name="accountingService">Service that manages balances and P&amp;L tracking.</param>
    /// <param name="settings">
    /// Options wrapper around <see cref="TradingSettings"/>; unwrapped to
    /// <see cref="TradingSettings"/> in the constructor for convenience.
    /// </param>
    /// <param name="logger">Logger instance for structured logging.</param>
    public AccountSyncWorker(
        IExchangeFactory exchangeFactory,
        IAccountingService accountingService,
        IOptions<TradingSettings> settings,
        ILogger<AccountSyncWorker> logger)
    {
        _exchangeFactory = exchangeFactory;
        _accountingService = accountingService;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Entry point for the background service. Loops indefinitely on the
    /// configured interval, fetching balances and running P&amp;L reconciliation
    /// for every configured exchange.
    /// </summary>
    /// <param name="stoppingToken">Token that signals graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AccountSyncWorker starting. Interval: {Interval}s", _settings.AccountSyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Delay first, then sync. This avoids racing against other startup
            // workers and gives the system time to establish exchange connections.
            await Task.Delay(TimeSpan.FromSeconds(_settings.AccountSyncIntervalSeconds), stoppingToken);

            // Process each exchange independently; a failure on one must not
            // prevent the others from being reconciled.
            foreach (var exchange in Enum.GetValues<ExchangeName>())
            {
                try
                {
                    // Resolve the exchange-specific client via the factory.
                    var client = _exchangeFactory.GetClient(exchange);

                    // Fetch the latest account balance (total equity, available margin, etc.).
                    var balance = await client.GetAccountBalanceAsync(stoppingToken);

                    // Push the fresh balance into the accounting service so it can
                    // update its internal ledger and compute exchange-side P&L.
                    _accountingService.UpdateBalance(exchange, balance);

                    // Compare locally tracked P&L against exchange-reported P&L.
                    // 'diverged' is true when the absolute difference exceeds the
                    // configured ReconciliationThreshold.
                    var (local, exchangePnL, diverged) = _accountingService.GetReconciliationReport(exchange);
                    if (diverged)
                    {
                        // Alert operators: local and exchange P&L have diverged beyond
                        // the acceptable threshold. Common causes include missed fills,
                        // fee discrepancies, or external account activity.
                        _logger.LogWarning(
                            "RECONCILIATION WARNING for {Exchange}: Local PnL={LocalPnL:F4}, Exchange PnL={ExchangePnL:F4}",
                            exchange, local.NetPnL, exchangePnL?.NetPnL);
                    }
                    else
                    {
                        _logger.LogDebug("Account sync OK for {Exchange}: Balance={Balance}", exchange, balance.TotalBalance);
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: log and continue. The exchange may be temporarily
                    // unreachable or not configured for this environment.
                    _logger.LogWarning(ex, "Failed to sync account for {Exchange}", exchange);
                }
            }
        }
    }
}
