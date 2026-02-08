using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges;

public interface IExchangeFactory
{
    IExchangeClient GetClient(ExchangeName exchange);
}
