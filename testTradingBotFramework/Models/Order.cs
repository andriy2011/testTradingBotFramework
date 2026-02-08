// -----------------------------------------------------------------------
// Order.cs
//
// Represents an order within the trading bot framework. Each order has
// both a locally generated identifier (OrderId) and an optional
// exchange-assigned identifier (ExchangeOrderId) that is populated
// after the order is successfully submitted to the exchange.
// Orders are created by OrderManager in response to incoming TradeSignals
// and track their lifecycle from Pending through to Filled or Rejected.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// Represents an order submitted (or pending submission) to an exchange.
/// <para>
/// The order lifecycle flows: Pending -> Submitted -> PartiallyFilled -> Filled (or Rejected/Cancelled).
/// <see cref="Price"/> is nullable because market orders execute at the best available price
/// and do not require a price to be specified.
/// <see cref="SignalId"/> links the order back to the <see cref="TradeSignal"/> that triggered its creation.
/// </para>
/// </summary>
public class Order
{
    /// <summary>
    /// Locally generated unique identifier for this order (32-character hex GUID without hyphens).
    /// Used for internal tracking and correlation throughout the framework.
    /// </summary>
    public string OrderId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The identifier assigned by the exchange after the order is submitted.
    /// Null until the exchange acknowledges the order. Used for querying order status
    /// and cancellation requests on the exchange.
    /// </summary>
    public string? ExchangeOrderId { get; set; }

    /// <summary>The target exchange where this order will be (or has been) placed.</summary>
    public ExchangeName Exchange { get; set; }

    /// <summary>The trading pair symbol (e.g., "BTCUSDT", "ETHUSDT").</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Whether this is a Buy or Sell order.</summary>
    public OrderSide Side { get; set; }

    /// <summary>The order type (e.g., Market, Limit) which determines execution behavior.</summary>
    public OrderType OrderType { get; set; }

    /// <summary>The desired quantity to trade, expressed in the base asset.</summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// The limit price for the order. Nullable because market orders do not require a price;
    /// they execute immediately at the best available market price.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// Current status of the order in its lifecycle.
    /// Defaults to <see cref="OrderStatus.Pending"/> when first created.
    /// </summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>
    /// The quantity that has been filled so far. Supports partial fills where
    /// only a portion of the requested quantity has been executed.
    /// </summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>
    /// The weighted average price at which fills have occurred.
    /// Null if no fills have happened yet. Updated as partial fills come in.
    /// </summary>
    public decimal? AverageFillPrice { get; set; }

    /// <summary>
    /// Links this order back to the <see cref="TradeSignal"/> that triggered its creation.
    /// Nullable because orders may be created manually or through other mechanisms.
    /// </summary>
    public string? SignalId { get; set; }

    /// <summary>UTC timestamp when the order was initially created in the framework.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// UTC timestamp of the most recent update to this order (e.g., status change, fill event).
    /// Null if the order has not been updated since creation.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
