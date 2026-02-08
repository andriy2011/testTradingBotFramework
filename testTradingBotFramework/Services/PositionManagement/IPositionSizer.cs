using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.PositionManagement;

public interface IPositionSizer
{
    Task<decimal> CalculateQuantityAsync(TradeSignal signal, CancellationToken ct = default);
}
