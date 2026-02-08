// =============================================================================
// OandaOrderMapperTests.cs
// Unit tests for OandaOrderMapper, the static mapper that converts between
// the framework's local Order model and Oanda's REST API request/response formats.
//
// The mapper provides two conversion directions:
//   1. ToOandaOrder (local Order -> OandaOrderRequest):
//      - Converts OrderSide to signed units (Buy = positive, Sell = negative)
//      - Maps OrderType to Oanda type strings ("MARKET", "LIMIT", "STOP")
//      - Sets TimeInForce ("FOK" for market, "GTC" for limit/stop orders)
//      - Includes price for limit and stop orders
//
//   2. ToLocalOrder (OandaOpenOrder -> local Order):
//      - Parses the units string to determine side and absolute quantity
//      - Maps Oanda type strings back to local OrderType enums
//      - Parses the price string to a decimal
//      - Always sets status to Submitted (open orders on Oanda are active)
//
// Oanda uses strings for numeric values in their API (units, price), which
// is why the mapper deals with string parsing/formatting.
// =============================================================================

using FluentAssertions;
using testTradingBotFramework.Exchanges.Oanda;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="OandaOrderMapper"/>, covering both the ToOandaOrder
/// (outbound mapping for placing orders) and ToLocalOrder (inbound mapping
/// for reading open orders from Oanda's API) directions.
/// No mocking needed — these are pure static mapping functions.
/// </summary>
public class OandaOrderMapperTests
{
    // =========================================================================
    // ToOandaOrder: Local Order -> OandaOrderRequest
    // =========================================================================

    // -------------------------------------------------------------------------
    // Side mapping: Buy/Sell -> positive/negative units
    // -------------------------------------------------------------------------

    /// <summary>
    /// Oanda uses signed units to represent order direction:
    /// positive units = Buy (go long), negative units = Sell (go short).
    /// A Buy order with Quantity=1000 should produce Units="1000".
    /// </summary>
    [Fact]
    public void ToOandaOrder_BuySide_PositiveUnits()
    {
        var order = new Order
        {
            Symbol = "EUR_USD",
            Side = OrderSide.Buy,
            Quantity = 1000m,
            OrderType = OrderType.Market
        };

        var result = OandaOrderMapper.ToOandaOrder(order);

        result.Order.Units.Should().Be("1000");         // positive = Buy
        result.Order.Instrument.Should().Be("EUR_USD");  // symbol passed through
    }

    /// <summary>
    /// A Sell order should negate the quantity to produce negative units.
    /// Sell with Quantity=1000 should produce Units="-1000".
    /// </summary>
    [Fact]
    public void ToOandaOrder_SellSide_NegativeUnits()
    {
        var order = new Order
        {
            Symbol = "EUR_USD",
            Side = OrderSide.Sell,
            Quantity = 1000m,
            OrderType = OrderType.Market
        };

        var result = OandaOrderMapper.ToOandaOrder(order);

        result.Order.Units.Should().Be("-1000"); // negative = Sell
    }

    // -------------------------------------------------------------------------
    // Order type mapping: Market, Limit, Stop + TimeInForce
    // -------------------------------------------------------------------------

    /// <summary>
    /// Market orders should have:
    ///   - Type = "MARKET"
    ///   - TimeInForce = "FOK" (Fill Or Kill — execute immediately or cancel)
    /// FOK is standard for forex market orders to prevent partial fills at stale prices.
    /// </summary>
    [Fact]
    public void ToOandaOrder_MarketOrder_TypeAndTimeInForce()
    {
        var order = new Order
        {
            Symbol = "EUR_USD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            OrderType = OrderType.Market
        };

        var result = OandaOrderMapper.ToOandaOrder(order);

        result.Order.Type.Should().Be("MARKET");
        result.Order.TimeInForce.Should().Be("FOK");
    }

    /// <summary>
    /// Limit orders should have:
    ///   - Type = "LIMIT"
    ///   - Price set to the order's limit price
    ///   - TimeInForce = "GTC" (Good Till Cancelled — stays active until filled or cancelled)
    /// </summary>
    [Fact]
    public void ToOandaOrder_LimitOrder_TypePriceAndTimeInForce()
    {
        var order = new Order
        {
            Symbol = "EUR_USD",
            Side = OrderSide.Buy,
            Quantity = 100m,
            OrderType = OrderType.Limit,
            Price = 1.105m
        };

        var result = OandaOrderMapper.ToOandaOrder(order);

        result.Order.Type.Should().Be("LIMIT");
        result.Order.Price.Should().Be("1.105");  // price formatted as string for Oanda API
        result.Order.TimeInForce.Should().Be("GTC");
    }

    /// <summary>
    /// StopMarket orders map to Oanda's "STOP" type with GTC time-in-force.
    /// Oanda doesn't distinguish between stop-market and stop-limit — both
    /// use the "STOP" type. StopLimit also maps to "STOP" in the source code.
    /// </summary>
    [Fact]
    public void ToOandaOrder_StopMarketOrder_MapsToStop()
    {
        var order = new Order
        {
            Symbol = "EUR_USD",
            Side = OrderSide.Sell,
            Quantity = 500m,
            OrderType = OrderType.StopMarket,
            Price = 1.09m
        };

        var result = OandaOrderMapper.ToOandaOrder(order);

        result.Order.Type.Should().Be("STOP");
        result.Order.TimeInForce.Should().Be("GTC");
    }

    // =========================================================================
    // ToLocalOrder: OandaOpenOrder -> Local Order
    // =========================================================================

    // -------------------------------------------------------------------------
    // Units sign -> Side mapping
    // -------------------------------------------------------------------------

    /// <summary>
    /// When Oanda returns an order with positive units, it represents a Buy order.
    /// The mapper should set Side = Buy and Quantity = absolute value of units.
    /// Also verifies that Exchange is always set to Oanda and other fields map correctly.
    /// </summary>
    [Fact]
    public void ToLocalOrder_PositiveUnits_BuySide()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "42",
            Instrument = "EUR_USD",
            Units = "1000",       // positive = Buy
            Type = "LIMIT",
            Price = "1.105"
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.Side.Should().Be(OrderSide.Buy);
        result.Quantity.Should().Be(1000m);
        result.Exchange.Should().Be(ExchangeName.Oanda);
        result.Symbol.Should().Be("EUR_USD");
        result.ExchangeOrderId.Should().Be("42");
    }

    /// <summary>
    /// When Oanda returns an order with negative units, it represents a Sell order.
    /// The mapper should set Side = Sell and Quantity = absolute value (no sign).
    /// </summary>
    [Fact]
    public void ToLocalOrder_NegativeUnits_SellSide()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "43",
            Instrument = "EUR_USD",
            Units = "-500",       // negative = Sell
            Type = "STOP",
            Price = "1.09"
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.Side.Should().Be(OrderSide.Sell);
        result.Quantity.Should().Be(500m);       // absolute value, no negative
    }

    // -------------------------------------------------------------------------
    // Type mapping: Oanda type strings -> local OrderType enum
    // -------------------------------------------------------------------------

    /// <summary>
    /// Oanda "LIMIT" type should map to local OrderType.Limit.
    /// </summary>
    [Fact]
    public void ToLocalOrder_LimitType_MapsToLimit()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "1",
            Instrument = "EUR_USD",
            Units = "100",
            Type = "LIMIT",
            Price = "1.10"
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.OrderType.Should().Be(OrderType.Limit);
    }

    /// <summary>
    /// Oanda "STOP" type should map to local OrderType.StopMarket.
    /// (The framework uses StopMarket as the generic stop order type.)
    /// </summary>
    [Fact]
    public void ToLocalOrder_StopType_MapsToStopMarket()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "1",
            Instrument = "EUR_USD",
            Units = "100",
            Type = "STOP",
            Price = "1.10"
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.OrderType.Should().Be(OrderType.StopMarket);
    }

    /// <summary>
    /// Any unrecognized Oanda order type (e.g., "MARKET_IF_TOUCHED", or future
    /// new types) should default to OrderType.Market as a safe fallback.
    /// </summary>
    [Fact]
    public void ToLocalOrder_UnknownType_MapsToMarket()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "1",
            Instrument = "EUR_USD",
            Units = "100",
            Type = "MARKET_IF_TOUCHED" // not explicitly handled -> defaults to Market
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.OrderType.Should().Be(OrderType.Market);
    }

    // -------------------------------------------------------------------------
    // Price parsing
    // -------------------------------------------------------------------------

    /// <summary>
    /// Oanda returns prices as strings (e.g., "1.10523"). The mapper parses
    /// them into decimal values using decimal.TryParse. Verifies that a valid
    /// price string is correctly converted.
    /// </summary>
    [Fact]
    public void ToLocalOrder_PriceParsing_SetsPrice()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "1",
            Instrument = "EUR_USD",
            Units = "100",
            Type = "LIMIT",
            Price = "1.10523"  // string price -> decimal 1.10523
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.Price.Should().Be(1.10523m);
    }

    // -------------------------------------------------------------------------
    // Status: always Submitted for open orders
    // -------------------------------------------------------------------------

    /// <summary>
    /// ToLocalOrder is only called for open (pending) orders fetched from
    /// Oanda's account endpoint. These are always in an active/submitted state,
    /// so the mapper unconditionally sets Status = Submitted.
    /// </summary>
    [Fact]
    public void ToLocalOrder_Status_AlwaysSubmitted()
    {
        var oandaOrder = new OandaOpenOrder
        {
            Id = "1",
            Instrument = "EUR_USD",
            Units = "100",
            Type = "LIMIT",
            Price = "1.10"
        };

        var result = OandaOrderMapper.ToLocalOrder(oandaOrder);

        result.Status.Should().Be(OrderStatus.Submitted);
    }
}
