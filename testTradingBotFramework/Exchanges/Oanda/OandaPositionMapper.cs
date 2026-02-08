// -----------------------------------------------------------------------
// OandaPositionMapper.cs
//
// Maps Oanda position data to the framework's unified Position model.
//
// Oanda represents positions differently from most exchanges: each
// instrument has a single position record that contains separate Long
// and Short sub-objects. This mapper splits each Oanda position into
// 0, 1, or 2 framework Position objects depending on which sides are
// active:
//   - Long.Units > 0  --> yields a Long position
//   - Short.Units < 0 --> yields a Short position (quantity stored as absolute value)
//   - Both can be active simultaneously (hedging mode)
//   - Neither active   --> yields nothing for that instrument
//
// Uses yield return for lazy/deferred enumeration, which integrates
// efficiently with LINQ's SelectMany in the caller.
// -----------------------------------------------------------------------

using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Oanda;

/// <summary>
/// Static mapper that converts Oanda position data into the framework's
/// unified <see cref="Position"/> model. Handles the Oanda-specific convention
/// of splitting each instrument into separate Long and Short sub-positions.
/// </summary>
public static class OandaPositionMapper
{
    /// <summary>
    /// Converts a single Oanda position (which covers one instrument) into zero, one,
    /// or two framework <see cref="Position"/> objects using lazy enumeration.
    /// </summary>
    /// <param name="oandaPos">
    /// The Oanda position containing Long and Short sub-objects for a single instrument.
    /// </param>
    /// <returns>
    /// An enumerable yielding a Long position if units &gt; 0, a Short position if
    /// units &lt; 0, both if the account is hedged, or nothing if the position is flat.
    /// </returns>
    public static IEnumerable<Position> ToLocalPositions(OandaPosition oandaPos)
    {
        // Check the long side: Oanda reports positive units for active long positions
        var longUnits = decimal.Parse(oandaPos.Long.Units);
        if (longUnits > 0)
        {
            yield return new Position
            {
                Exchange = ExchangeName.Oanda,
                Symbol = oandaPos.Instrument,
                Side = PositionSide.Long,
                Quantity = longUnits,
                // AveragePrice and UnrealizedPL may be null/empty when the side has no trades,
                // so TryParse is used defensively with a fallback of 0
                EntryPrice = decimal.TryParse(oandaPos.Long.AveragePrice, out var lp) ? lp : 0,
                UnrealizedPnL = decimal.TryParse(oandaPos.Long.UnrealizedPL, out var lpl) ? lpl : 0
            };
        }

        // Check the short side: Oanda reports negative units for active short positions
        var shortUnits = decimal.Parse(oandaPos.Short.Units);
        if (shortUnits < 0)
        {
            yield return new Position
            {
                Exchange = ExchangeName.Oanda,
                Symbol = oandaPos.Instrument,
                Side = PositionSide.Short,
                // Store quantity as absolute value since the framework uses Side to indicate direction
                Quantity = Math.Abs(shortUnits),
                EntryPrice = decimal.TryParse(oandaPos.Short.AveragePrice, out var sp) ? sp : 0,
                UnrealizedPnL = decimal.TryParse(oandaPos.Short.UnrealizedPL, out var spl) ? spl : 0
            };
        }
    }
}
