using Binance.Net.Objects.Models.Futures;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Binance;

public static class BinancePositionMapper
{
    public static Position ToLocalPosition(BinancePositionDetailsUsdt pos)
    {
        var quantity = Math.Abs(pos.Quantity);
        var side = pos.Quantity >= 0 ? PositionSide.Long : PositionSide.Short;

        return new Position
        {
            Exchange = ExchangeName.Binance,
            Symbol = pos.Symbol,
            Side = side,
            Quantity = quantity,
            EntryPrice = pos.EntryPrice,
            CurrentPrice = pos.MarkPrice,
            UnrealizedPnL = pos.UnrealizedPnl
        };
    }
}
