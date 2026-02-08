using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Oanda;

public static class OandaPositionMapper
{
    public static IEnumerable<Position> ToLocalPositions(OandaPosition oandaPos)
    {
        var longUnits = decimal.Parse(oandaPos.Long.Units);
        if (longUnits > 0)
        {
            yield return new Position
            {
                Exchange = ExchangeName.Oanda,
                Symbol = oandaPos.Instrument,
                Side = PositionSide.Long,
                Quantity = longUnits,
                EntryPrice = decimal.TryParse(oandaPos.Long.AveragePrice, out var lp) ? lp : 0,
                UnrealizedPnL = decimal.TryParse(oandaPos.Long.UnrealizedPL, out var lpl) ? lpl : 0
            };
        }

        var shortUnits = decimal.Parse(oandaPos.Short.Units);
        if (shortUnits < 0)
        {
            yield return new Position
            {
                Exchange = ExchangeName.Oanda,
                Symbol = oandaPos.Instrument,
                Side = PositionSide.Short,
                Quantity = Math.Abs(shortUnits),
                EntryPrice = decimal.TryParse(oandaPos.Short.AveragePrice, out var sp) ? sp : 0,
                UnrealizedPnL = decimal.TryParse(oandaPos.Short.UnrealizedPL, out var spl) ? spl : 0
            };
        }
    }
}
