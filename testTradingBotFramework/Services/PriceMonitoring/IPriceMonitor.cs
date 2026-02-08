// =============================================================================
// IPriceMonitor.cs
// Real-time price feed abstraction. Supports subscribe/unsubscribe per symbol,
// caches latest prices, and fires events on each tick.
//
// Implementations:
//   - BinancePriceMonitor: WebSocket book ticker (per-symbol subscriptions)
//   - OandaPriceMonitor: SSE streaming (single stream, restarts on symbol change)
//
// Resolved via keyed DI by ExchangeName.
// =============================================================================

namespace testTradingBotFramework.Services.PriceMonitoring;

/// <summary>
/// Real-time price feed for a specific exchange. Provides subscribe/unsubscribe,
/// cached latest prices, and an event for each price tick.
/// </summary>
public interface IPriceMonitor
{
    /// <summary>Starts receiving price updates for a symbol. No-op if already subscribed.</summary>
    Task SubscribeAsync(string symbol, CancellationToken ct = default);

    /// <summary>Stops receiving price updates for a symbol and clears cached price.</summary>
    Task UnsubscribeAsync(string symbol, CancellationToken ct = default);

    /// <summary>Returns the most recent cached price, or <c>null</c> if not subscribed.</summary>
    PriceUpdateEventArgs? GetLatestPrice(string symbol);

    /// <summary>Returns all cached prices across all subscribed symbols.</summary>
    IReadOnlyDictionary<string, PriceUpdateEventArgs> GetAllPrices();

    /// <summary>Fires on each price tick. Used by PriceMonitorWorker to update position P&amp;L.</summary>
    event EventHandler<PriceUpdateEventArgs>? OnPriceUpdate;
}
