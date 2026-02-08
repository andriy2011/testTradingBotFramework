// -----------------------------------------------------------------------
// TradeSignal.cs
//
// Represents an inbound trade signal received from Azure Event Hub.
// Parsed by SignalParser from JSON and dispatched by SignalDispatcher
// to OrderManager for execution. Quantity and Price are intentionally
// nullable: when Quantity is null, OrderManager delegates to
// FixedFractionPositionSizer to calculate an appropriate size; when
// Price is null, a market order is assumed (no price needed).
// The Metadata dictionary provides extensibility for strategy-specific
// key-value pairs without requiring schema changes.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// An inbound trade signal that instructs the framework to take a trading action.
/// <para>
/// Signals are received from Azure Event Hub, parsed from JSON by <c>SignalParser</c>,
/// and dispatched by <c>SignalDispatcher</c> to <c>OrderManager</c> for order creation and execution.
/// </para>
/// </summary>
public class TradeSignal
{
    /// <summary>
    /// Locally generated unique identifier for this signal (32-character hex GUID without hyphens).
    /// Propagated to <see cref="Order.SignalId"/> and <see cref="TradeRecord.SignalId"/>
    /// for end-to-end traceability.
    /// </summary>
    public string SignalId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>The target exchange where the signal should be executed.</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>The trading pair symbol (e.g., "BTCUSDT", "ETHUSDT").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>The action to perform (e.g., Open a new position, Close an existing one).</summary>
    public SignalAction Action { get; set; }

    /// <summary>The order side: Buy or Sell.</summary>
    public OrderSide Side { get; set; }

    /// <summary>
    /// The order type (e.g., Market, Limit). Defaults to Market for immediate execution.
    /// </summary>
    public OrderType OrderType { get; set; } = OrderType.Market;

    /// <summary>The asset class this signal targets (e.g., Spot, Futures).</summary>
    public AssetClass AssetClass { get; set; }

    /// <summary>
    /// The desired trade quantity in base asset units.
    /// Nullable: when null, OrderManager uses FixedFractionPositionSizer to
    /// automatically calculate the appropriate position size based on account balance.
    /// </summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// The desired price for limit orders.
    /// Nullable: market orders do not require a price as they execute at the best available price.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>The UTC timestamp when this signal was generated.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Extensible key-value pairs for strategy-specific data (e.g., "stopLoss", "takeProfit",
    /// "strategyName", "confidence"). Allows signals to carry additional context without
    /// requiring changes to the core model schema.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
