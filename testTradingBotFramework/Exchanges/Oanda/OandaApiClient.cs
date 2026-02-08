using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;

namespace testTradingBotFramework.Exchanges.Oanda;

public class OandaApiClient
{
    private readonly HttpClient _httpClient;
    private readonly OandaSettings _settings;
    private readonly ILogger<OandaApiClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OandaApiClient(HttpClient httpClient, IOptions<OandaSettings> settings, ILogger<OandaApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_settings.RestBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
        _httpClient.DefaultRequestHeaders.Add("Accept-Datetime-Format", "RFC3339");
    }

    public async Task<OandaOrderResponse?> PlaceOrderAsync(OandaOrderRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"/v3/accounts/{_settings.AccountId}/orders", request, JsonOptions, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Oanda order failed ({Status}): {Body}", response.StatusCode, body);
            return null;
        }

        return JsonSerializer.Deserialize<OandaOrderResponse>(body, JsonOptions);
    }

    public async Task<OandaAccountResponse?> GetAccountAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"/v3/accounts/{_settings.AccountId}", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get Oanda account: {Status}", response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OandaAccountResponse>(body, JsonOptions);
    }

    public async Task<OandaPricingResponse?> GetPricingAsync(string instruments, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync(
            $"/v3/accounts/{_settings.AccountId}/pricing?instruments={Uri.EscapeDataString(instruments)}", ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to get Oanda pricing: {Status}", response.StatusCode);
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<OandaPricingResponse>(body, JsonOptions);
    }

    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsync(
            $"/v3/accounts/{_settings.AccountId}/orders/{orderId}/cancel", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to cancel Oanda order {OrderId}: {Status}", orderId, response.StatusCode);
            return false;
        }

        return true;
    }

    public HttpClient GetStreamHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(_settings.StreamBaseUrl),
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
        return client;
    }

    public string GetStreamUrl(string instruments)
    {
        return $"/v3/accounts/{_settings.AccountId}/pricing/stream?instruments={Uri.EscapeDataString(instruments)}";
    }
}
