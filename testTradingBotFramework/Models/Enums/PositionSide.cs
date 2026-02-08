// -----------------------------------------------------------------------
// <file>
//   PositionSide.cs - Direction of an open position (long or short).
//   Determined from signed quantities on both Binance and Oanda.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Direction of an open position.
/// </summary>
/// <remarks>
/// Determined from signed quantities: positive quantities indicate a
/// <see cref="Long"/> position, negative quantities indicate a
/// <see cref="Short"/> position. This convention applies to both
/// Binance and Oanda position data.
/// </remarks>
public enum PositionSide
{
    /// <summary>
    /// Long position - profits when the instrument's price increases.
    /// Represented by a positive quantity.
    /// </summary>
    Long,

    /// <summary>
    /// Short position - profits when the instrument's price decreases.
    /// Represented by a negative quantity.
    /// </summary>
    Short
}
