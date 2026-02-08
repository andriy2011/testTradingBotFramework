// -----------------------------------------------------------------------
// PnLSnapshot.cs
//
// Point-in-time profit and loss (P&L) summary for a specific exchange.
// Used by AccountingService for local P&L tracking and reconciliation
// against exchange-reported values. NetPnL is a computed property that
// combines realized gains, unrealized floating gains, and subtracts
// accumulated trading fees.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// A point-in-time snapshot of profit and loss (P&amp;L) for a single exchange account.
/// <para>
/// <see cref="NetPnL"/> is a computed property: realized + unrealized - fees.
/// This model is used by the AccountingService to maintain a local ledger and
/// to reconcile against the P&amp;L values reported by the exchange itself.
/// </para>
/// </summary>
public class PnLSnapshot
{
    /// <summary>The exchange this P&amp;L snapshot pertains to.</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>
    /// Cumulative profit or loss from positions that have been closed.
    /// This value is locked in and does not change with market fluctuations.
    /// </summary>
    public decimal RealizedPnL { get; set; }

    /// <summary>
    /// Floating profit or loss from positions that are still open.
    /// This value fluctuates with current market prices.
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>Total trading fees accumulated across all executed trades.</summary>
    public decimal TotalFees { get; set; }

    /// <summary>
    /// Net profit and loss computed as: <see cref="RealizedPnL"/> + <see cref="UnrealizedPnL"/> - <see cref="TotalFees"/>.
    /// This represents the true bottom-line performance after accounting for all costs.
    /// </summary>
    public decimal NetPnL => RealizedPnL + UnrealizedPnL - TotalFees;

    /// <summary>The total number of trades executed during the tracking period.</summary>
    public int TotalTrades { get; set; }

    /// <summary>The UTC timestamp when this P&amp;L snapshot was captured.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
