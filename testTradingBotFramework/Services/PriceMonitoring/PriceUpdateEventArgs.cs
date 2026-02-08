// =============================================================================
// PriceUpdateEventArgs.cs
// Event data for real-time price updates from IPriceMonitor implementations.
// Contains best bid/ask from the exchange with a computed mid-price.
// Used by PriceMonitorWorker to update position unrealized P&L and by
// DashboardRenderer to display live bid/ask/spread.
// =============================================================================

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PriceMonitoring;

/// <summary>
/// Event arguments for price update events from <see cref="IPriceMonitor"/>.
/// Immutable after construction (init-only properties).
/// </summary>
public class PriceUpdateEventArgs : EventArgs
{
    /// <summary>Which exchange this price came from.</summary>
    public ExchangeName Exchange { get; init; }

    /// <summary>The trading instrument symbol (e.g., "BTCUSDT", "EUR_USD").</summary>
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Best bid price (highest price a buyer is willing to pay).</summary>
    public decimal Bid { get; init; }

    /// <summary>Best ask price (lowest price a seller is willing to accept).</summary>
    public decimal Ask { get; init; }

    /// <summary>Mid-price: arithmetic mean of bid and ask. Used for position P&amp;L calculation.</summary>
    public decimal Mid => (Bid + Ask) / 2m;

    /// <summary>When this price was received. Used for staleness detection in the dashboard.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
