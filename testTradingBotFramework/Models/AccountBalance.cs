// -----------------------------------------------------------------------
// AccountBalance.cs
//
// Represents a point-in-time snapshot of an exchange account's balance.
// Used by AccountSyncWorker to periodically pull balance data from
// exchanges, and by FixedFractionPositionSizer to determine how much
// capital is available for new positions.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// Captures the balance state of a trading account on a specific exchange at a given moment.
/// <para>
/// <see cref="TotalBalance"/> reflects the full wallet balance (including margin used by open positions).
/// <see cref="AvailableBalance"/> is the margin available for opening new positions
/// (i.e., total balance minus margin currently locked in open positions).
/// <see cref="UnrealizedPnL"/> tracks the floating profit or loss from positions that have not yet been closed.
/// </para>
/// </summary>
public class AccountBalance
{
    /// <summary>The exchange this balance belongs to (e.g., Binance, Bybit).</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>The currency/asset denomination of the balance (e.g., "USDT", "BTC").</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Full wallet balance including margin locked in open positions.
    /// This is the gross account value before considering available vs. in-use margin.
    /// </summary>
    public decimal TotalBalance { get; set; }

    /// <summary>
    /// Margin available for opening new positions.
    /// Calculated as: TotalBalance minus margin currently used by open positions.
    /// </summary>
    public decimal AvailableBalance { get; set; }

    /// <summary>
    /// Floating profit or loss from currently open positions.
    /// Positive values indicate unrealized gains; negative values indicate unrealized losses.
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>The UTC timestamp when this balance snapshot was captured.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
