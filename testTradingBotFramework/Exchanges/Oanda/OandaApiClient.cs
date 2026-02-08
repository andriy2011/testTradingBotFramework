// -----------------------------------------------------------------------
// OandaApiClient.cs
//
// Low-level HTTP client for the Oanda REST v3 API.
// Wraps HttpClient with Bearer token authentication and RFC3339 datetime
// formatting. Provides methods for order placement, account retrieval,
// pricing queries, order cancellation, and streaming price feeds.
//
// This class is designed to be registered via AddHttpClient<OandaApiClient>()
// so that the HttpClient instance is managed by the DI container's
// IHttpClientFactory. A separate long-lived HttpClient is created by
// GetStreamHttpClient() for Server-Sent Events (SSE) streaming connections.
//
// All API methods return null (or false) on HTTP errors -- the caller is
// responsible for handling failure cases and retries.
// -----------------------------------------------------------------------

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;

namespace testTradingBotFramework.Exchanges.Oanda;

/// <summary>
/// Low-level HTTP client that communicates directly with the Oanda REST v3 API.
/// Handles authentication, serialization, and HTTP transport concerns.
/// Higher-level business logic is handled by <see cref="OandaExchangeClient"/>.
/// </summary>
public class OandaApiClient
{
    /// <summary>Typed HttpClient injected by the DI container via IHttpClientFactory.</summary>
    private readonly HttpClient _httpClient;

    /// <summary>Oanda-specific configuration (API token, account ID, base URLs).</summary>
    private readonly OandaSettings _settings;

    /// <summary>Structured logger for recording API call outcomes and errors.</summary>
    private readonly ILogger<OandaApiClient> _logger;

    /// <summary>
    /// Shared JSON serializer options used for all request/response serialization.
    /// Case-insensitive property matching accommodates Oanda's camelCase JSON responses.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes the Oanda API client with a pre-configured HttpClient.
    /// Configures the base address, Bearer token authentication header, and
    /// the Accept-Datetime-Format header so all timestamps use RFC3339 format.
    /// </summary>
    /// <param name="httpClient">HttpClient instance managed by IHttpClientFactory.</param>
    /// <param name="settings">Oanda configuration options (token, account ID, URLs).</param>
    /// <param name="logger">Logger for recording API errors and diagnostics.</param>
    public OandaApiClient(HttpClient httpClient, IOptions<OandaSettings> settings, ILogger<OandaApiClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        // Configure the HttpClient for all subsequent Oanda REST API calls
        _httpClient.BaseAddress = new Uri(_settings.RestBaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
        // Request RFC3339 timestamps (e.g., "2024-01-15T10:30:00.000000000Z") instead of UNIX epoch
        _httpClient.DefaultRequestHeaders.Add("Accept-Datetime-Format", "RFC3339");
    }

    /// <summary>
    /// Submits an order to the Oanda v3 orders endpoint.
    /// The response may contain an OrderFillTransaction (market orders filled immediately),
    /// an OrderCreateTransaction (pending/limit orders accepted), or an OrderCancelTransaction
    /// (order rejected by the broker).
    /// </summary>
    /// <param name="request">The Oanda-specific order request payload.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Deserialized order response, or null if the HTTP request failed.</returns>
    public async Task<OandaOrderResponse?> PlaceOrderAsync(OandaOrderRequest request, CancellationToken ct = default)
    {
        // POST the order request as JSON to the Oanda v3 orders endpoint
        var response = await _httpClient.PostAsJsonAsync(
            $"/v3/accounts/{_settings.AccountId}/orders", request, JsonOptions, ct);

        // Read the response body regardless of status code (needed for error logging)
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Log the full response body to aid in diagnosing order rejections
            _logger.LogError("Oanda order failed ({Status}): {Body}", response.StatusCode, body);
            return null;
        }

        return JsonSerializer.Deserialize<OandaOrderResponse>(body, JsonOptions);
    }

    /// <summary>
    /// Retrieves the full account details including balance, positions, and open orders.
    /// This is the primary endpoint for reading account state from Oanda.
    /// </summary>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Deserialized account response, or null if the request failed.</returns>
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

    /// <summary>
    /// Fetches current bid/ask pricing for one or more instruments (e.g., "EUR_USD,GBP_USD").
    /// Instrument names use Oanda's underscore-separated format and are URL-encoded for safety.
    /// </summary>
    /// <param name="instruments">Comma-separated list of instrument names (e.g., "EUR_USD").</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Deserialized pricing response containing bid/ask arrays, or null on failure.</returns>
    public async Task<OandaPricingResponse?> GetPricingAsync(string instruments, CancellationToken ct = default)
    {
        // URL-encode instruments to handle any special characters in instrument names
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

    /// <summary>
    /// Cancels an existing pending order by its Oanda order ID.
    /// Uses HTTP PUT with an empty body per the Oanda v3 API specification.
    /// </summary>
    /// <param name="orderId">The Oanda-assigned order identifier to cancel.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>True if the cancellation succeeded; false if the API returned an error.</returns>
    public async Task<bool> CancelOrderAsync(string orderId, CancellationToken ct = default)
    {
        // Oanda v3 uses PUT (not DELETE) with null body to cancel orders
        var response = await _httpClient.PutAsync(
            $"/v3/accounts/{_settings.AccountId}/orders/{orderId}/cancel", null, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to cancel Oanda order {OrderId}: {Status}", orderId, response.StatusCode);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a separate long-lived HttpClient configured for SSE (Server-Sent Events)
    /// streaming connections. This client uses a different base URL (stream endpoint)
    /// and has an infinite timeout since streaming connections remain open indefinitely.
    /// </summary>
    /// <remarks>
    /// A dedicated HttpClient is needed because streaming connections must not be pooled
    /// or recycled by IHttpClientFactory, and they require the stream-specific base URL
    /// rather than the REST API base URL.
    /// </remarks>
    /// <returns>A new HttpClient configured for streaming with Bearer token auth.</returns>
    public HttpClient GetStreamHttpClient()
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(_settings.StreamBaseUrl),
            // Streaming connections stay open indefinitely, so disable the default 100s timeout
            Timeout = Timeout.InfiniteTimeSpan
        };
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiToken}");
        return client;
    }

    /// <summary>
    /// Builds the relative URL for the Oanda pricing stream endpoint.
    /// Used in combination with <see cref="GetStreamHttpClient"/> to establish
    /// a streaming connection for real-time price updates.
    /// </summary>
    /// <param name="instruments">Comma-separated instrument names (e.g., "EUR_USD,GBP_USD").</param>
    /// <returns>Relative URL path for the pricing stream endpoint.</returns>
    public string GetStreamUrl(string instruments)
    {
        return $"/v3/accounts/{_settings.AccountId}/pricing/stream?instruments={Uri.EscapeDataString(instruments)}";
    }
}
