// -----------------------------------------------------------------------
// TradeRecord.cs
//
// Immutable record of a completed trade fill. Created by OrderManager
// after successful order execution and stored in ITradeHistoryStore.
// Serves as the audit trail for all executed trades and is used by
// AccountingService for P&L calculation and reconciliation.
// Each record links back to both the local OrderId and the exchange's
// ExchangeOrderId for full cross-reference traceability.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// An immutable record of a single trade fill (execution) on an exchange.
/// <para>
/// Created after an order is successfully executed and persisted to
/// <c>ITradeHistoryStore</c> for accounting, auditing, and P&amp;L calculation.
/// Maintains references to both local and exchange identifiers for complete traceability.
/// </para>
/// </summary>
public class TradeRecord
{
    /// <summary>
    /// Locally generated unique identifier for this trade record (32-character hex GUID without hyphens).
    /// </summary>
    public string TradeId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The local <see cref="Order.OrderId"/> that this trade fill belongs to.
    /// Used for correlating fills back to the originating order.
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// The exchange-assigned order identifier. Nullable because it may not be available
    /// in all scenarios. Used for cross-referencing with exchange trade history.
    /// </summary>
    public string? ExchangeOrderId { get; set; }

    /// <summary>
    /// The <see cref="TradeSignal.SignalId"/> that originally triggered the order.
    /// Nullable because not all trades originate from signals.
    /// Enables end-to-end traceability: Signal -> Order -> Trade.
    /// </summary>
    public string? SignalId { get; set; }

    /// <summary>The exchange where this trade was executed.</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>The trading pair symbol (e.g., "BTCUSDT").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Whether this was a Buy or Sell execution.</summary>
    public OrderSide Side { get; set; }

    /// <summary>The quantity that was filled in this trade, in base asset units.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The price at which this fill was executed.</summary>
    public decimal Price { get; set; }

    /// <summary>The trading fee charged by the exchange for this fill.</summary>
    public decimal Fee { get; set; }

    /// <summary>The asset in which the fee was denominated (e.g., "BNB", "USDT").</summary>
    public string FeeAsset { get; set; } = string.Empty;

    /// <summary>The UTC timestamp when this trade was executed on the exchange.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
