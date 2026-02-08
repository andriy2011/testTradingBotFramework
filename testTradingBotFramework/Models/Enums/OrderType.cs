// -----------------------------------------------------------------------
// <file>
//   OrderType.cs - Types of orders supported by the framework.
//   Each type maps to exchange-specific order types on Binance and Oanda.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Types of orders supported by the framework.
/// </summary>
/// <remarks>
/// Exchange-specific mappings:
/// <list type="bullet">
///   <item>
///     <term>Binance</term>
///     <description>StopLimit maps to "Stop"; StopMarket maps to "StopMarket".</description>
///   </item>
///   <item>
///     <term>Oanda</term>
///     <description>Both StopLimit and StopMarket map to "STOP".</description>
///   </item>
/// </list>
/// </remarks>
public enum OrderType
{
    /// <summary>
    /// Execute immediately at the best available price.
    /// No price parameter is required.
    /// </summary>
    Market,

    /// <summary>
    /// Execute at the specified price or better.
    /// The order remains open until filled, cancelled, or expired.
    /// </summary>
    Limit,

    /// <summary>
    /// Triggers a market order when the specified stop price is reached.
    /// On Binance this maps to "StopMarket"; on Oanda this maps to "STOP".
    /// </summary>
    StopMarket,

    /// <summary>
    /// Triggers a limit order when the specified stop price is reached.
    /// On Binance this maps to "Stop"; on Oanda this maps to "STOP".
    /// </summary>
    StopLimit
}
