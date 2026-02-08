// =============================================================================
// IAccountingService.cs
// Trade accounting and P&L reconciliation service. Tracks trade history,
// exchange balances, local vs exchange P&L, and detects divergence.
//
// Implemented by: AccountingService
// =============================================================================

using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.Accounting;

/// <summary>
/// Provides trade recording, balance tracking, P&amp;L snapshot generation,
/// and reconciliation between locally-computed and exchange-reported P&amp;L.
/// </summary>
public interface IAccountingService
{
    /// <summary>Persists a completed trade fill to the <see cref="ITradeHistoryStore"/>.</summary>
    Task RecordTradeAsync(TradeRecord trade);

    /// <summary>Stores the latest exchange-reported account balance for reconciliation.</summary>
    void UpdateBalance(ExchangeName exchange, AccountBalance balance);

    /// <summary>Computes P&amp;L from local trade history and open position unrealized P&amp;L.</summary>
    PnLSnapshot GetLocalPnLSnapshot(ExchangeName exchange);

    /// <summary>Returns exchange-reported P&amp;L, or <c>null</c> if no balance data available.</summary>
    PnLSnapshot? GetExchangePnLSnapshot(ExchangeName exchange);

    /// <summary>
    /// Compares local vs exchange P&amp;L. Returns <c>Diverged = true</c> if the absolute
    /// difference exceeds <c>ReconciliationThreshold</c>.
    /// </summary>
    (PnLSnapshot Local, PnLSnapshot? Exchange, bool Diverged) GetReconciliationReport(ExchangeName exchange);

    /// <summary>Queries trade history with optional exchange, symbol, and limit filters.</summary>
    Task<IReadOnlyList<TradeRecord>> GetTradeHistoryAsync(ExchangeName? exchange = null, string? symbol = null, int? limit = null);
}
