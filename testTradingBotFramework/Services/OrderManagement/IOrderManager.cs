using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.OrderManagement;

public interface IOrderManager
{
    Task ExecuteSignalAsync(TradeSignal signal, CancellationToken ct = default);
}
