using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.EventProcessing;

public interface ISignalDispatcher
{
    Task DispatchAsync(TradeSignal signal, CancellationToken ct = default);
}
