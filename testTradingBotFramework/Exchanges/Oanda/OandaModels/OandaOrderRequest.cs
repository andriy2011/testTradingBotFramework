// -----------------------------------------------------------------------
// <file>
//   OandaOrderRequest.cs - Oanda v3 API request body for
//   POST /v3/accounts/{id}/orders. Built by OandaOrderMapper.ToOandaOrder().
//   Units are signed strings (positive = buy, negative = sell).
// </file>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

/// <summary>
/// Top-level request body for the Oanda v3 POST /v3/accounts/{id}/orders endpoint.
/// Wraps the <see cref="OandaOrderBody"/> in the "order" JSON key as required by the API.
/// </summary>
/// <remarks>
/// Constructed by <c>OandaOrderMapper.ToOandaOrder()</c> from the framework's
/// internal <c>OrderRequest</c> model.
/// </remarks>
public class OandaOrderRequest
{
    /// <summary>
    /// The order body containing type, instrument, units, and optional price/time-in-force.
    /// </summary>
    [JsonPropertyName("order")]
    public OandaOrderBody Order { get; set; } = new();
}

/// <summary>
/// The order details sent to the Oanda v3 API.
/// </summary>
/// <remarks>
/// <para>
/// Units are signed strings: positive values represent a buy, negative values represent a sell.
/// </para>
/// <para>
/// <see cref="Price"/> and <see cref="TimeInForce"/> are nullable and excluded from
/// the serialized JSON when null (market orders do not require them).
/// </para>
/// </remarks>
public class OandaOrderBody
{
    /// <summary>
    /// Oanda order type string (e.g., "MARKET", "LIMIT", "STOP").
    /// Defaults to "MARKET".
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "MARKET";

    /// <summary>
    /// The instrument to trade (e.g., "EUR_USD").
    /// </summary>
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// Number of units to trade as a signed string.
    /// Positive = buy, negative = sell.
    /// </summary>
    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    /// <summary>
    /// The price for limit or stop orders. Null for market orders.
    /// Excluded from JSON when null.
    /// </summary>
    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Price { get; set; }

    /// <summary>
    /// Time-in-force policy (e.g., "GTC", "GFD", "FOK").
    /// Null for market orders. Excluded from JSON when null.
    /// </summary>
    [JsonPropertyName("timeInForce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeInForce { get; set; }
}
