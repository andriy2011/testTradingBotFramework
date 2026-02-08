using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.EventProcessing;

public interface ISignalParser
{
    TradeSignal? Parse(string rawMessage);
}
