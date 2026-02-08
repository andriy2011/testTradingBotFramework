// -----------------------------------------------------------------------
// <file>
//   OrderSide.cs - Direction of a trade order (buy or sell).
//   Mapped to exchange-specific representations by each order mapper.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Direction of a trade order.
/// </summary>
/// <remarks>
/// Mapped to exchange-specific representations by:
/// <list type="bullet">
///   <item><c>BinanceOrderMapper</c> - maps to Binance's Buy/Sell enum values.</item>
///   <item><c>OandaOrderMapper</c> - maps to positive (buy) or negative (sell) unit strings.</item>
/// </list>
/// </remarks>
public enum OrderSide
{
    /// <summary>
    /// Buy / go long - increases position size or opens a long position.
    /// </summary>
    Buy,

    /// <summary>
    /// Sell / go short - decreases position size or opens a short position.
    /// </summary>
    Sell
}
