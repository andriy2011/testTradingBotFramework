// =============================================================================
// BinanceOrderMapper.cs
// Static mapper that converts between the framework's local enum types and
// the Binance.Net library's enum types. This decouples the framework's domain
// model from the Binance SDK, making it easier to evolve either side independently.
//
// Three conversion methods:
//   1. ToOrderSide: local OrderSide -> Binance.Net OrderSide
//   2. ToFuturesOrderType: local OrderType -> Binance.Net FuturesOrderType
//   3. ToLocalOrderStatus: Binance.Net OrderStatus -> local OrderStatus
//
// Notable mapping details:
//   - StopLimit -> Binance "Stop" (Binance uses "Stop" for stop-limit on futures,
//     while "StopMarket" is for market stop orders)
//   - Binance "New" -> local "Submitted" (Binance calls accepted orders "New")
//   - Binance "Canceled" (US) -> local "Cancelled" (UK spelling)
//   - Unknown statuses default to "Pending" (safe fallback for new/unknown values)
// =============================================================================

using BinanceEnums = Binance.Net.Enums;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Binance;

/// <summary>
/// Provides static mapping methods between the framework's domain enums
/// and Binance.Net library enums. Used by <see cref="BinanceExchangeClient"/>
/// when placing orders and interpreting responses.
/// </summary>
public static class BinanceOrderMapper
{
    /// <summary>
    /// Maps the framework's <see cref="OrderSide"/> to Binance's OrderSide.
    /// Used when constructing order placement requests.
    /// </summary>
    /// <param name="side">The local order side (Buy or Sell).</param>
    /// <returns>The corresponding Binance OrderSide enum value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown OrderSide values.</exception>
    public static BinanceEnums.OrderSide ToOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => BinanceEnums.OrderSide.Buy,
        OrderSide.Sell => BinanceEnums.OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side))
    };

    /// <summary>
    /// Maps the framework's <see cref="OrderType"/> to Binance's FuturesOrderType.
    /// Note: StopLimit maps to "Stop" (not "StopMarket") because Binance uses
    /// "Stop" for stop-limit orders on futures, while "StopMarket" is for
    /// stop orders that execute at market price once the stop is triggered.
    /// </summary>
    /// <param name="type">The local order type.</param>
    /// <returns>The corresponding Binance FuturesOrderType enum value.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for unknown OrderType values.</exception>
    public static BinanceEnums.FuturesOrderType ToFuturesOrderType(OrderType type) => type switch
    {
        OrderType.Market => BinanceEnums.FuturesOrderType.Market,
        OrderType.Limit => BinanceEnums.FuturesOrderType.Limit,
        OrderType.StopMarket => BinanceEnums.FuturesOrderType.StopMarket,
        OrderType.StopLimit => BinanceEnums.FuturesOrderType.Stop,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    /// <summary>
    /// Maps a Binance OrderStatus to the framework's local <see cref="Models.Enums.OrderStatus"/>.
    /// This is the reverse direction — used when reading order results and open orders
    /// from the Binance API.
    ///
    /// Key mappings:
    ///   - "New" -> "Submitted" (Binance's term for a newly accepted order)
    ///   - "Canceled" -> "Cancelled" (US vs UK spelling normalization)
    ///   - Unknown/unmapped statuses -> "Pending" (safe default for future Binance additions)
    /// </summary>
    /// <param name="status">The Binance order status.</param>
    /// <returns>The corresponding local OrderStatus enum value.</returns>
    public static Models.Enums.OrderStatus ToLocalOrderStatus(BinanceEnums.OrderStatus status) => status switch
    {
        BinanceEnums.OrderStatus.New => Models.Enums.OrderStatus.Submitted,
        BinanceEnums.OrderStatus.PartiallyFilled => Models.Enums.OrderStatus.PartiallyFilled,
        BinanceEnums.OrderStatus.Filled => Models.Enums.OrderStatus.Filled,
        BinanceEnums.OrderStatus.Canceled => Models.Enums.OrderStatus.Cancelled,
        BinanceEnums.OrderStatus.Rejected => Models.Enums.OrderStatus.Rejected,
        BinanceEnums.OrderStatus.Expired => Models.Enums.OrderStatus.Expired,
        _ => Models.Enums.OrderStatus.Pending // Safe default for unknown statuses
    };
}
