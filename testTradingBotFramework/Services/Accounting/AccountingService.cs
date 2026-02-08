// =============================================================================
// AccountingService.cs
// Centralized accounting layer that aggregates trade history, balance data,
// and position P&L into unified snapshots and reconciliation reports.
//
// Responsibilities:
//   - RecordTradeAsync: Persists executed trades to the trade history store
//   - UpdateBalance: Stores the latest exchange-reported account balance
//   - GetLocalPnLSnapshot: Computes P&L from local data (trade fees + position P&L)
//   - GetExchangePnLSnapshot: Returns the exchange-reported P&L
//   - GetReconciliationReport: Compares local vs exchange P&L and flags divergences
//
// The reconciliation report is used by the dashboard to show whether local
// tracking is in sync with the exchange. Divergences above the configured
// threshold trigger warnings for manual investigation.
// =============================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Services.Accounting;

/// <summary>
/// Aggregates data from <see cref="ITradeHistoryStore"/> and <see cref="IPositionManager"/>
/// to produce P&L snapshots and reconciliation reports. Also acts as a thin
/// delegation layer for recording trades.
/// </summary>
public class AccountingService : IAccountingService
{
    private readonly ITradeHistoryStore _tradeStore;
    private readonly IPositionManager _positionManager;

    /// <summary>
    /// Stores the latest account balance snapshot from each exchange.
    /// Updated periodically by the AccountSyncWorker.
    /// ConcurrentDictionary is used because updates come from background workers.
    /// </summary>
    private readonly ConcurrentDictionary<ExchangeName, AccountBalance> _exchangeBalances = new();

    private readonly TradingSettings _settings;
    private readonly ILogger<AccountingService> _logger;

    public AccountingService(
        ITradeHistoryStore tradeStore,
        IPositionManager positionManager,
        IOptions<TradingSettings> settings,
        ILogger<AccountingService> logger)
    {
        _tradeStore = tradeStore;
        _positionManager = positionManager;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Records an executed trade by delegating to the trade history store.
    /// Adds structured logging for audit trail purposes.
    /// Called by OrderManager after a successful fill.
    /// </summary>
    /// <param name="trade">The trade record to persist.</param>
    public async Task RecordTradeAsync(TradeRecord trade)
    {
        await _tradeStore.AddAsync(trade);
        _logger.LogInformation("Trade recorded: {TradeId} {Exchange}:{Symbol} {Side} {Qty}@{Price} Fee={Fee}",
            trade.TradeId, trade.Exchange, trade.Symbol, trade.Side, trade.Quantity, trade.Price, trade.Fee);
    }

    /// <summary>
    /// Stores the latest exchange-reported account balance. Overwrites any
    /// previous balance for the same exchange. Called by AccountSyncWorker
    /// on each sync cycle.
    /// </summary>
    /// <param name="exchange">The exchange this balance belongs to.</param>
    /// <param name="balance">The account balance snapshot from the exchange API.</param>
    public void UpdateBalance(ExchangeName exchange, AccountBalance balance)
    {
        _exchangeBalances[exchange] = balance;
        _logger.LogDebug("Balance updated for {Exchange}: Total={Total}, Available={Available}",
            exchange, balance.TotalBalance, balance.AvailableBalance);
    }

    /// <summary>
    /// Computes a P&L snapshot from local data by:
    ///   1. Summing all trade fees from the trade history store for this exchange
    ///   2. Summing unrealized P&L from all open positions for this exchange
    ///   3. Counting total trades for this exchange
    ///
    /// Note: RealizedPnL is set to 0 because the framework currently only tracks
    /// entry fills. Realized P&L calculation will be added when exit tracking is implemented.
    /// </summary>
    /// <param name="exchange">The exchange to compute the snapshot for.</param>
    /// <returns>A P&L snapshot computed from local trade and position data.</returns>
    public PnLSnapshot GetLocalPnLSnapshot(ExchangeName exchange)
    {
        // Sum fees from all trades on this exchange
        var trades = _tradeStore.GetAll(exchange);
        var totalFees = trades.Sum(t => t.Fee);

        // Sum unrealized P&L from all open positions on this exchange
        var positions = _positionManager.GetOpenPositions(exchange);
        var unrealizedPnL = positions.Sum(p => p.UnrealizedPnL);

        return new PnLSnapshot
        {
            Exchange = exchange,
            RealizedPnL = 0, // Entry-only for now; realized P&L computed when exits are added
            UnrealizedPnL = unrealizedPnL,
            TotalFees = totalFees,
            TotalTrades = trades.Count
        };
    }

    /// <summary>
    /// Returns a P&L snapshot derived from the exchange-reported account balance.
    /// Returns null if no balance has been stored yet (e.g., before the first
    /// AccountSyncWorker cycle completes).
    /// </summary>
    /// <param name="exchange">The exchange to get the snapshot for.</param>
    /// <returns>A P&L snapshot from exchange data, or null if no balance is available.</returns>
    public PnLSnapshot? GetExchangePnLSnapshot(ExchangeName exchange)
    {
        if (!_exchangeBalances.TryGetValue(exchange, out var balance))
            return null;

        return new PnLSnapshot
        {
            Exchange = exchange,
            UnrealizedPnL = balance.UnrealizedPnL,
            Timestamp = balance.Timestamp
        };
    }

    /// <summary>
    /// Generates a reconciliation report comparing local P&L with exchange-reported P&L.
    /// Flags divergence if the absolute difference in unrealized P&L exceeds the
    /// configured <see cref="TradingSettings.ReconciliationThreshold"/>.
    ///
    /// Used by the dashboard to show sync status and alert operators to potential issues
    /// like missed fills, manual trades, or calculation errors.
    /// </summary>
    /// <param name="exchange">The exchange to reconcile.</param>
    /// <returns>
    /// A tuple containing:
    /// - Local: the locally-computed P&L snapshot
    /// - Exchange: the exchange-reported P&L snapshot (null if no balance stored)
    /// - Diverged: true if the difference exceeds the threshold
    /// </returns>
    public (PnLSnapshot Local, PnLSnapshot? Exchange, bool Diverged) GetReconciliationReport(ExchangeName exchange)
    {
        var local = GetLocalPnLSnapshot(exchange);
        var exchangeSnapshot = GetExchangePnLSnapshot(exchange);

        bool diverged = false;
        if (exchangeSnapshot is not null)
        {
            // Compare unrealized P&L: |local - exchange| > threshold
            var diff = Math.Abs(local.UnrealizedPnL - exchangeSnapshot.UnrealizedPnL);
            if (diff > _settings.ReconciliationThreshold)
            {
                diverged = true;
                _logger.LogWarning(
                    "P&L divergence detected for {Exchange}: Local={LocalPnL}, Exchange={ExchangePnL}, Diff={Diff}",
                    exchange, local.UnrealizedPnL, exchangeSnapshot.UnrealizedPnL, diff);
            }
        }

        return (local, exchangeSnapshot, diverged);
    }

    /// <summary>
    /// Queries the trade history store with optional filters.
    /// Delegates directly to <see cref="ITradeHistoryStore.QueryAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<TradeRecord>> GetTradeHistoryAsync(
        ExchangeName? exchange = null, string? symbol = null, int? limit = null)
    {
        return await _tradeStore.QueryAsync(exchange, symbol, limit);
    }
}
