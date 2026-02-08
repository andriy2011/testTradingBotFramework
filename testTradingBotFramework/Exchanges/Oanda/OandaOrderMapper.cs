// =============================================================================
// OandaOrderMapper.cs
// Static mapper that converts between the framework's local Order model and
// Oanda's REST API request/response formats.
//
// Two conversion directions:
//
// 1. ToOandaOrder (outbound: local Order -> OandaOrderRequest):
//    - Converts OrderSide to signed units (Buy = positive, Sell = negative)
//      because Oanda uses signed quantities to represent direction
//    - Maps OrderType to Oanda type strings: "MARKET", "LIMIT", "STOP"
//    - Sets TimeInForce: "FOK" (Fill Or Kill) for market orders,
//      "GTC" (Good Till Cancelled) for limit and stop orders
//    - Formats numeric values as strings (Oanda API convention)
//
// 2. ToLocalOrder (inbound: OandaOpenOrder -> local Order):
//    - Parses the signed units string to determine side and absolute quantity
//    - Maps Oanda type strings back to local OrderType enums
//    - Parses price strings to decimal values
//    - Always sets status to Submitted (open orders on Oanda are active)
//    - Always sets Exchange to Oanda
// =============================================================================

using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Oanda;

/// <summary>
/// Provides static mapping methods between the framework's <see cref="Order"/> model
/// and Oanda's API request/response formats. Used by <see cref="OandaExchangeClient"/>
/// for order placement and open order retrieval.
/// </summary>
public static class OandaOrderMapper
{
    /// <summary>
    /// Converts a local <see cref="Order"/> to an <see cref="OandaOrderRequest"/>
    /// suitable for Oanda's REST API.
    ///
    /// Oanda conventions:
    ///   - Units are signed: positive = buy, negative = sell
    ///   - Numeric values are strings (e.g., "1000", "1.105")
    ///   - Order types: "MARKET", "LIMIT", "STOP"
    ///   - TimeInForce: "FOK" for market (immediate fill or cancel),
    ///     "GTC" for limit/stop (remain active until filled or cancelled)
    /// </summary>
    /// <param name="order">The local order to convert.</param>
    /// <returns>An OandaOrderRequest ready for the Oanda API.</returns>
    public static OandaOrderRequest ToOandaOrder(Order order)
    {
        // Convert to signed units: positive for Buy, negative for Sell
        var units = order.Quantity;
        if (order.Side == OrderSide.Sell)
            units = -units;

        var body = new OandaOrderBody
        {
            Instrument = order.Symbol,
            Units = units.ToString("G") // "G" format avoids trailing zeros
        };

        // Map order type and set appropriate TimeInForce and price fields
        switch (order.OrderType)
        {
            case OrderType.Market:
                body.Type = "MARKET";
                body.TimeInForce = "FOK"; // Fill Or Kill — execute immediately or cancel
                break;
            case OrderType.Limit:
                body.Type = "LIMIT";
                body.Price = order.Price?.ToString("G");
                body.TimeInForce = "GTC"; // Good Till Cancelled
                break;
            case OrderType.StopMarket:
                // Oanda uses "STOP" for both stop-market and stop-limit
                body.Type = "STOP";
                body.Price = order.Price?.ToString("G");
                body.TimeInForce = "GTC";
                break;
            case OrderType.StopLimit:
                // Same as StopMarket for Oanda — both map to "STOP"
                body.Type = "STOP";
                body.Price = order.Price?.ToString("G");
                body.TimeInForce = "GTC";
                break;
        }

        return new OandaOrderRequest { Order = body };
    }

    /// <summary>
    /// Converts an <see cref="OandaOpenOrder"/> from Oanda's account response
    /// to a local <see cref="Order"/> object.
    ///
    /// Parsing rules:
    ///   - Signed units -> Side: positive/zero = Buy, negative = Sell
    ///   - Quantity is the absolute value of units
    ///   - Type mapping: "LIMIT" -> Limit, "STOP" -> StopMarket, else -> Market
    ///   - Price is parsed from string to decimal via TryParse
    ///   - Exchange is always set to Oanda
    ///   - Status is always Submitted (open orders are active on Oanda)
    /// </summary>
    /// <param name="oandaOrder">The Oanda open order to convert.</param>
    /// <returns>A local Order object with mapped fields.</returns>
    public static Order ToLocalOrder(OandaOpenOrder oandaOrder)
    {
        // Parse signed units string to determine side and absolute quantity
        var units = decimal.Parse(oandaOrder.Units);
        return new Order
        {
            ExchangeOrderId = oandaOrder.Id,
            Exchange = ExchangeName.Oanda,
            Symbol = oandaOrder.Instrument,
            Side = units >= 0 ? OrderSide.Buy : OrderSide.Sell, // sign determines direction
            OrderType = oandaOrder.Type switch
            {
                "LIMIT" => OrderType.Limit,
                "STOP" => OrderType.StopMarket,
                _ => OrderType.Market // safe default for unknown types
            },
            Quantity = Math.Abs(units), // always store as positive
            Price = decimal.TryParse(oandaOrder.Price, out var p) ? p : null,
            Status = Models.Enums.OrderStatus.Submitted // open orders on Oanda are always active
        };
    }
}
