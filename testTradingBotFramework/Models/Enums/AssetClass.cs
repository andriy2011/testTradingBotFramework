// -----------------------------------------------------------------------
// <file>
//   AssetClass.cs - Categorizes the asset type for a trade signal.
//   Used in TradeSignal to indicate which market the signal targets.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Categorizes the asset type for a trade signal, determining which market
/// and exchange integration the signal targets.
/// </summary>
/// <remarks>
/// Used by <c>TradeSignal</c> to route orders to the correct exchange client
/// and apply the appropriate symbol formatting and order conventions.
/// </remarks>
public enum AssetClass
{
    /// <summary>
    /// Binance USD-M perpetual futures contracts.
    /// Traded via the Binance Futures API with leverage and margin requirements.
    /// </summary>
    CryptoFutures,

    /// <summary>
    /// Cryptocurrency spot trading (e.g., BTC/USDT on Binance spot market).
    /// Not currently implemented; reserved for future use.
    /// </summary>
    CryptoSpot,

    /// <summary>
    /// Foreign exchange (FX) currency pairs traded via Oanda.
    /// Instruments use Oanda's underscore format (e.g., "EUR_USD").
    /// </summary>
    Forex
}
