// =============================================================================
// OandaPriceMonitor.cs
// Real-time price monitor for Oanda forex using Server-Sent Events (SSE) streaming.
//
// Architecture:
//   - Unlike Binance (per-symbol WebSocket), Oanda uses a single HTTP stream
//     for all instruments, specified as a comma-separated query parameter
//   - When symbols are added/removed, the entire stream must be restarted
//     with the updated instrument list (RestartStreamAsync)
//   - RunStreamAsync reads JSON lines, filters for Type=="PRICE" (ignoring
//     HEARTBEAT messages), parses bid/ask, and fires OnPriceUpdate
//   - Auto-reconnects with 5-second backoff on disconnection
//
// Thread safety:
//   - Lock (_symbolLock) protects the _subscribedSymbols HashSet
//   - ConcurrentDictionary for _latestPrices cache
//   - CancellationTokenSource for clean stream teardown
//
// Implements IDisposable to cancel the stream on shutdown.
// =============================================================================

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges.Oanda;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PriceMonitoring.Oanda;

/// <summary>
/// Oanda SSE streaming price monitor. Manages a single HTTP streaming connection
/// for all subscribed instruments, with automatic reconnection on failure.
/// </summary>
public class OandaPriceMonitor : IPriceMonitor, IDisposable
{
    private readonly OandaApiClient _apiClient;
    private readonly OandaSettings _settings;
    private readonly ILogger<OandaPriceMonitor> _logger;

    /// <summary>Cache of the most recent price update per symbol.</summary>
    private readonly ConcurrentDictionary<string, PriceUpdateEventArgs> _latestPrices = new();

    /// <summary>Set of currently subscribed instrument symbols (e.g., "EUR_USD").</summary>
    private readonly HashSet<string> _subscribedSymbols = [];

    /// <summary>Lock protecting _subscribedSymbols (HashSet is not thread-safe).</summary>
    private readonly Lock _symbolLock = new();

    /// <summary>Cancellation source for the active streaming task.</summary>
    private CancellationTokenSource? _streamCts;

    /// <summary>The currently running streaming task (reads JSON lines from Oanda).</summary>
    private Task? _streamTask;

    public event EventHandler<PriceUpdateEventArgs>? OnPriceUpdate;

    public OandaPriceMonitor(OandaApiClient apiClient, IOptions<OandaSettings> settings, ILogger<OandaPriceMonitor> logger)
    {
        _apiClient = apiClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SubscribeAsync(string symbol, CancellationToken ct = default)
    {
        bool needsRestart;
        lock (_symbolLock)
        {
            needsRestart = _subscribedSymbols.Add(symbol);
        }

        if (needsRestart)
        {
            _logger.LogInformation("Subscribing to Oanda price feed for {Symbol}", symbol);
            await RestartStreamAsync(ct);
        }
    }

    public async Task UnsubscribeAsync(string symbol, CancellationToken ct = default)
    {
        bool needsRestart;
        lock (_symbolLock)
        {
            needsRestart = _subscribedSymbols.Remove(symbol);
        }

        _latestPrices.TryRemove(symbol, out _);

        if (needsRestart)
        {
            await RestartStreamAsync(ct);
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

    private async Task RestartStreamAsync(CancellationToken ct)
    {
        if (_streamCts is not null)
        {
            await _streamCts.CancelAsync();
            if (_streamTask is not null)
            {
                try { await _streamTask; } catch (OperationCanceledException) { }
            }
        }

        string[] symbols;
        lock (_symbolLock)
        {
            symbols = _subscribedSymbols.ToArray();
        }

        if (symbols.Length == 0) return;

        _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _streamTask = RunStreamAsync(symbols, _streamCts.Token);
    }

    private async Task RunStreamAsync(string[] symbols, CancellationToken ct)
    {
        var instruments = string.Join(",", symbols);
        var streamUrl = _apiClient.GetStreamUrl(instruments);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var client = _apiClient.GetStreamHttpClient();
                using var response = await client.GetStreamAsync(streamUrl, ct);
                using var reader = new StreamReader(response);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var data = JsonSerializer.Deserialize<OandaPricingStreamLine>(line);
                        if (data is null || data.Type != "PRICE" || data.Instrument is null) continue;
                        if (data.Bids is null || data.Bids.Count == 0 || data.Asks is null || data.Asks.Count == 0) continue;

                        var update = new PriceUpdateEventArgs
                        {
                            Exchange = ExchangeName.Oanda,
                            Symbol = data.Instrument,
                            Bid = decimal.Parse(data.Bids[0].Price),
                            Ask = decimal.Parse(data.Asks[0].Price),
                            Timestamp = DateTimeOffset.UtcNow
                        };

                        _latestPrices[data.Instrument] = update;
                        OnPriceUpdate?.Invoke(this, update);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogDebug(ex, "Failed to parse Oanda stream line");
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Oanda price stream disconnected. Reconnecting in 5s...");
                try { await Task.Delay(5000, ct); } catch (OperationCanceledException) { break; }
            }
        }
    }

    public void Dispose()
    {
        _streamCts?.Cancel();
        _streamCts?.Dispose();
    }
}
