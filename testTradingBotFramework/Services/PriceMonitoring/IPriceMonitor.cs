namespace testTradingBotFramework.Services.PriceMonitoring;

public interface IPriceMonitor
{
    Task SubscribeAsync(string symbol, CancellationToken ct = default);
    Task UnsubscribeAsync(string symbol, CancellationToken ct = default);
    PriceUpdateEventArgs? GetLatestPrice(string symbol);
    IReadOnlyDictionary<string, PriceUpdateEventArgs> GetAllPrices();
    event EventHandler<PriceUpdateEventArgs>? OnPriceUpdate;
}
