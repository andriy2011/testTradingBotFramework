// -----------------------------------------------------------------------
// Position.cs
//
// Represents an open position held on an exchange. Managed by
// PositionManager and updated by PriceMonitorWorker as new market
// prices arrive. The UpdateCurrentPrice method recalculates unrealized
// P&L based on position direction:
//   - Long positions profit when price rises:  (current - entry) * qty
//   - Short positions profit when price falls: (entry - current) * qty
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// Represents an open trading position on an exchange.
/// <para>
/// Positions are created when orders are filled and removed when fully closed.
/// The <see cref="UpdateCurrentPrice"/> method should be called whenever a new
/// market price is received to keep <see cref="UnrealizedPnL"/> accurate.
/// </para>
/// </summary>
public class Position
{
    /// <summary>
    /// Locally generated unique identifier for this position (32-character hex GUID without hyphens).
    /// </summary>
    public string PositionId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The exchange where this position is held.</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>The trading pair symbol (e.g., "BTCUSDT").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>The direction of the position: Long (expecting price to rise) or Short (expecting price to fall).</summary>
    public PositionSide Side { get; set; }

    /// <summary>The size of the position in base asset units.</summary>
    public decimal Quantity { get; set; }

    /// <summary>The weighted average price at which the position was entered.</summary>
    public decimal EntryPrice { get; set; }

    /// <summary>The most recently observed market price for this symbol.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>
    /// Floating profit or loss based on the difference between <see cref="CurrentPrice"/>
    /// and <see cref="EntryPrice"/>, adjusted for position <see cref="Side"/> and <see cref="Quantity"/>.
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>UTC timestamp when the position was first opened.</summary>
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the last price update. Null if never updated after opening.</summary>
    public DateTimeOffset? LastUpdatedAt { get; set; }

    /// <summary>
    /// Updates the current market price and recalculates unrealized P&amp;L.
    /// <para>
    /// For Long positions: P&amp;L = (price - EntryPrice) * Quantity (profit when price rises).
    /// For Short positions: P&amp;L = (EntryPrice - price) * Quantity (profit when price falls).
    /// </para>
    /// </summary>
    /// <param name="price">The latest market price for this position's symbol.</param>
    public void UpdateCurrentPrice(decimal price)
    {
        CurrentPrice = price;

        // Long: profit when price goes up; Short: profit when price goes down
        UnrealizedPnL = Side == PositionSide.Long
            ? (price - EntryPrice) * Quantity
            : (EntryPrice - price) * Quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }
}
