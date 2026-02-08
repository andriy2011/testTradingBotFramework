using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PositionManagement;

public interface IPositionManager
{
    void RecordFill(ExchangeName exchange, string symbol, OrderSide side, decimal quantity, decimal price);
    void SyncPositions(ExchangeName exchange, IReadOnlyList<Position> exchangePositions);
    bool ValidateRiskLimits(ExchangeName exchange);
    IReadOnlyList<Position> GetOpenPositions(ExchangeName? exchange = null);
    void UpdatePositionPrice(ExchangeName exchange, string symbol, decimal currentPrice);
}
