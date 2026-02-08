// -----------------------------------------------------------------------
// <file>
//   OandaPricingResponse.cs - Oanda v3 API pricing models for both
//   REST snapshot (GET /v3/accounts/{id}/pricing) and streaming endpoints.
//   OandaExchangeClient uses mid-price (best bid + best ask / 2).
//   OandaPriceMonitor filters streaming lines for Type == "PRICE".
// </file>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

/// <summary>
/// Response from the Oanda v3 GET /v3/accounts/{id}/pricing endpoint.
/// Contains a snapshot of current prices for the requested instruments.
/// </summary>
public class OandaPricingResponse
{
    /// <summary>
    /// List of price snapshots, one per requested instrument.
    /// </summary>
    [JsonPropertyName("prices")]
    public List<OandaPrice> Prices { get; set; } = [];
}

/// <summary>
/// Price data for a single instrument from the Oanda REST pricing endpoint.
/// Contains bid/ask depth levels and a timestamp.
/// </summary>
/// <remarks>
/// <c>OandaExchangeClient</c> calculates the mid-price as (best bid + best ask) / 2
/// using the first entry in each of the <see cref="Asks"/> and <see cref="Bids"/> lists.
/// </remarks>
public class OandaPrice
{
    /// <summary>
    /// The instrument identifier (e.g., "EUR_USD").
    /// </summary>
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// Ask (offer) price levels, ordered by increasing price. The first entry is the best ask.
    /// </summary>
    [JsonPropertyName("asks")]
    public List<OandaPriceLevel> Asks { get; set; } = [];

    /// <summary>
    /// Bid price levels, ordered by decreasing price. The first entry is the best bid.
    /// </summary>
    [JsonPropertyName("bids")]
    public List<OandaPriceLevel> Bids { get; set; } = [];

    /// <summary>
    /// Timestamp of the price quote in RFC 3339 format.
    /// </summary>
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// A single price level in the order book at a specific liquidity depth.
/// </summary>
public class OandaPriceLevel
{
    /// <summary>
    /// The price at this liquidity level (string for decimal precision).
    /// </summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    /// <summary>
    /// The available liquidity (number of units) at this price level.
    /// </summary>
    [JsonPropertyName("liquidity")]
    public int Liquidity { get; set; }
}

/// <summary>
/// A single line from the Oanda v3 streaming pricing endpoint.
/// Each line is a JSON object with a <see cref="Type"/> field that is either
/// "PRICE" (contains instrument pricing data) or "HEARTBEAT" (keep-alive signal).
/// </summary>
/// <remarks>
/// <c>OandaPriceMonitor</c> filters for lines where <see cref="Type"/> equals "PRICE"
/// and ignores heartbeat messages. Nullable fields are only populated for PRICE-type lines.
/// </remarks>
public class OandaPricingStreamLine
{
    /// <summary>
    /// The line type: "PRICE" for price updates or "HEARTBEAT" for keep-alive signals.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The instrument identifier (e.g., "EUR_USD"). Null for heartbeat messages.
    /// </summary>
    [JsonPropertyName("instrument")]
    public string? Instrument { get; set; }

    /// <summary>
    /// Ask price levels for this instrument. Null for heartbeat messages.
    /// </summary>
    [JsonPropertyName("asks")]
    public List<OandaPriceLevel>? Asks { get; set; }

    /// <summary>
    /// Bid price levels for this instrument. Null for heartbeat messages.
    /// </summary>
    [JsonPropertyName("bids")]
    public List<OandaPriceLevel>? Bids { get; set; }

    /// <summary>
    /// Timestamp of the price update in RFC 3339 format. Null for heartbeat messages.
    /// </summary>
    [JsonPropertyName("time")]
    public string? Time { get; set; }
}
