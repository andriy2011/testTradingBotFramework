using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges;

public class ExchangeFactory : IExchangeFactory
{
    private readonly IServiceProvider _serviceProvider;

    public ExchangeFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IExchangeClient GetClient(ExchangeName exchange)
    {
        return _serviceProvider.GetRequiredKeyedService<IExchangeClient>(exchange);
    }
}
