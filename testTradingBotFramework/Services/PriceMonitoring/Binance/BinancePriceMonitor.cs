using System.Collections.Concurrent;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PriceMonitoring.Binance;

public class BinancePriceMonitor : IPriceMonitor
{
    private readonly IBinanceSocketClient _socketClient;
    private readonly ILogger<BinancePriceMonitor> _logger;
    private readonly ConcurrentDictionary<string, PriceUpdateEventArgs> _latestPrices = new();
    private readonly ConcurrentDictionary<string, UpdateSubscription> _subscriptions = new();

    public event EventHandler<PriceUpdateEventArgs>? OnPriceUpdate;

    public BinancePriceMonitor(IBinanceSocketClient socketClient, ILogger<BinancePriceMonitor> logger)
    {
        _socketClient = socketClient;
        _logger = logger;
    }

    public async Task SubscribeAsync(string symbol, CancellationToken ct = default)
    {
        if (_subscriptions.ContainsKey(symbol))
        {
            _logger.LogDebug("Already subscribed to {Symbol} on Binance", symbol);
            return;
        }

        var result = await _socketClient.UsdFuturesApi.ExchangeData.SubscribeToBookTickerUpdatesAsync(
            symbol,
            data =>
            {
                var update = new PriceUpdateEventArgs
                {
                    Exchange = ExchangeName.Binance,
                    Symbol = symbol,
                    Bid = data.Data.BestBidPrice,
                    Ask = data.Data.BestAskPrice,
                    Timestamp = DateTimeOffset.UtcNow
                };

                _latestPrices[symbol] = update;
                OnPriceUpdate?.Invoke(this, update);
            });

        if (result.Success)
        {
            _subscriptions[symbol] = result.Data;
            _logger.LogInformation("Subscribed to Binance price feed for {Symbol}", symbol);
        }
        else
        {
            _logger.LogError("Failed to subscribe to Binance price feed for {Symbol}: {Error}",
                symbol, result.Error?.Message);
        }
    }

    public async Task UnsubscribeAsync(string symbol, CancellationToken ct = default)
    {
        if (_subscriptions.TryRemove(symbol, out var subscription))
        {
            await subscription.CloseAsync();
            _latestPrices.TryRemove(symbol, out _);
            _logger.LogInformation("Unsubscribed from Binance price feed for {Symbol}", symbol);
        }
    }

    public PriceUpdateEventArgs? GetLatestPrice(string symbol)
    {
        return _latestPrices.GetValueOrDefault(symbol);
    }

    public IReadOnlyDictionary<string, PriceUpdateEventArgs> GetAllPrices()
    {
        return _latestPrices;
    }
}
