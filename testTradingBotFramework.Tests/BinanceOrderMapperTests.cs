// =============================================================================
// BinanceOrderMapperTests.cs
// Unit tests for the BinanceOrderMapper static class, which converts between
// the framework's local enum types and Binance.Net library enum types.
//
// The mapper provides three conversion methods:
//   1. ToOrderSide:        local OrderSide -> Binance OrderSide
//   2. ToFuturesOrderType: local OrderType -> Binance FuturesOrderType
//   3. ToLocalOrderStatus: Binance OrderStatus -> local OrderStatus
//
// These are pure functions with no dependencies, so no mocking is needed.
// Theory/InlineData is used for parameterized testing of all known mappings.
//
// Important mapping details:
//   - StopLimit maps to Binance's "Stop" (not "StopMarket")
//   - Binance "New" status maps to local "Submitted"
//   - Binance "Canceled" (US spelling) maps to local "Cancelled" (UK spelling)
//   - Any unknown Binance status falls through to "Pending" (default case)
// =============================================================================

using FluentAssertions;
using BinanceEnums = Binance.Net.Enums;
using testTradingBotFramework.Exchanges.Binance;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="BinanceOrderMapper"/> — the static mapper that bridges
/// the gap between the framework's domain enums and the Binance.Net library enums.
/// These mappings are critical for correct order placement and status tracking.
/// </summary>
public class BinanceOrderMapperTests
{
    // -------------------------------------------------------------------------
    // ToOrderSide: local OrderSide -> Binance OrderSide
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that each local OrderSide value (Buy, Sell) maps to the
    /// corresponding Binance.Net OrderSide enum value. This mapping is used
    /// when constructing Binance API requests for order placement.
    /// </summary>
    [Theory]
    [InlineData(OrderSide.Buy, BinanceEnums.OrderSide.Buy)]
    [InlineData(OrderSide.Sell, BinanceEnums.OrderSide.Sell)]
    public void ToOrderSide_MapsCorrectly(OrderSide input, BinanceEnums.OrderSide expected)
    {
        BinanceOrderMapper.ToOrderSide(input).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // ToFuturesOrderType: local OrderType -> Binance FuturesOrderType
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that each local OrderType maps to the correct Binance futures
    /// order type. Notable mapping:
    ///   - StopLimit -> Binance "Stop" (Binance uses "Stop" for stop-limit orders
    ///     on futures, while "StopMarket" is for stop-market orders)
    /// </summary>
    [Theory]
    [InlineData(OrderType.Market, BinanceEnums.FuturesOrderType.Market)]
    [InlineData(OrderType.Limit, BinanceEnums.FuturesOrderType.Limit)]
    [InlineData(OrderType.StopMarket, BinanceEnums.FuturesOrderType.StopMarket)]
    [InlineData(OrderType.StopLimit, BinanceEnums.FuturesOrderType.Stop)]
    public void ToFuturesOrderType_MapsCorrectly(OrderType input, BinanceEnums.FuturesOrderType expected)
    {
        BinanceOrderMapper.ToFuturesOrderType(input).Should().Be(expected);
    }

    // -------------------------------------------------------------------------
    // ToLocalOrderStatus: Binance OrderStatus -> local OrderStatus
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies that each known Binance order status maps to the correct local
    /// OrderStatus enum value. Key mappings:
    ///   - "New" -> "Submitted" (Binance calls newly accepted orders "New")
    ///   - "Canceled" -> "Cancelled" (US vs UK spelling difference)
    ///   - All other statuses map directly by name
    /// </summary>
    [Theory]
    [InlineData(BinanceEnums.OrderStatus.New, Models.Enums.OrderStatus.Submitted)]
    [InlineData(BinanceEnums.OrderStatus.PartiallyFilled, Models.Enums.OrderStatus.PartiallyFilled)]
    [InlineData(BinanceEnums.OrderStatus.Filled, Models.Enums.OrderStatus.Filled)]
    [InlineData(BinanceEnums.OrderStatus.Canceled, Models.Enums.OrderStatus.Cancelled)]
    [InlineData(BinanceEnums.OrderStatus.Rejected, Models.Enums.OrderStatus.Rejected)]
    [InlineData(BinanceEnums.OrderStatus.Expired, Models.Enums.OrderStatus.Expired)]
    public void ToLocalOrderStatus_MapsCorrectly(BinanceEnums.OrderStatus input, Models.Enums.OrderStatus expected)
    {
        BinanceOrderMapper.ToLocalOrderStatus(input).Should().Be(expected);
    }

    /// <summary>
    /// Any Binance OrderStatus not explicitly mapped (e.g., "Insurance", "Adl",
    /// or future new status values) should fall through to the default case
    /// and return OrderStatus.Pending. This is a safe default because unknown
    /// statuses indicate the order's final state is not yet determined.
    /// </summary>
    [Fact]
    public void ToLocalOrderStatus_UnknownStatus_DefaultsToPending()
    {
        // "Insurance" is an unusual Binance status used for insurance fund liquidations
        var result = BinanceOrderMapper.ToLocalOrderStatus(BinanceEnums.OrderStatus.Insurance);

        result.Should().Be(Models.Enums.OrderStatus.Pending);
    }
}
