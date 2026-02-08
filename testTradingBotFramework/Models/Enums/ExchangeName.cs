// -----------------------------------------------------------------------
// <file>
//   ExchangeName.cs - Identifies the target exchange for order routing
//   and dependency injection resolution.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Identifies the target exchange for order routing and service resolution.
/// </summary>
/// <remarks>
/// Used as the keyed dependency injection key for resolving
/// <c>IExchangeClient</c> and <c>IPriceMonitor</c> implementations.
/// Also serves as the position book key in <c>PositionManager</c> to
/// segregate positions by exchange.
/// </remarks>
public enum ExchangeName
{
    /// <summary>
    /// Binance exchange - supports USD-M futures via the Binance Futures API.
    /// Resolved to <c>BinanceExchangeClient</c> and <c>BinancePriceMonitor</c>.
    /// </summary>
    Binance,

    /// <summary>
    /// Oanda exchange - supports forex (FX) pairs via the Oanda v3 REST API.
    /// Resolved to <c>OandaExchangeClient</c> and <c>OandaPriceMonitor</c>.
    /// </summary>
    Oanda
}
