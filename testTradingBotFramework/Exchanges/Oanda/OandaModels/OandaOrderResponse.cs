// -----------------------------------------------------------------------
// <file>
//   OandaOrderResponse.cs - Oanda v3 API response models for
//   POST /v3/accounts/{id}/orders. Contains three mutually exclusive
//   transaction types: fill, cancel, and create. OandaExchangeClient
//   checks them in priority order: fill > cancel > create.
// </file>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

/// <summary>
/// Response from the Oanda v3 POST /v3/accounts/{id}/orders endpoint.
/// Contains three mutually exclusive transaction types; only one is populated per response.
/// </summary>
/// <remarks>
/// <c>OandaExchangeClient</c> checks the transaction types in priority order:
/// <list type="number">
///   <item><see cref="OrderFillTransaction"/> - market order was filled immediately.</item>
///   <item><see cref="OrderCancelTransaction"/> - order was rejected with a reason.</item>
///   <item><see cref="OrderCreateTransaction"/> - pending order was accepted, will be filled later.</item>
/// </list>
/// </remarks>
public class OandaOrderResponse
{
    /// <summary>
    /// Present when a market order was filled immediately.
    /// Contains fill price, units, P&L, and commission details.
    /// </summary>
    [JsonPropertyName("orderFillTransaction")]
    public OandaFillTransaction? OrderFillTransaction { get; set; }

    /// <summary>
    /// Present when the order was rejected by Oanda.
    /// Contains the rejection reason.
    /// </summary>
    [JsonPropertyName("orderCancelTransaction")]
    public OandaCancelTransaction? OrderCancelTransaction { get; set; }

    /// <summary>
    /// Present when a pending order (limit/stop) was accepted and is awaiting execution.
    /// Contains only the assigned order ID.
    /// </summary>
    [JsonPropertyName("orderCreateTransaction")]
    public OandaCreateTransaction? OrderCreateTransaction { get; set; }
}

/// <summary>
/// Transaction details for an immediately filled market order on Oanda.
/// </summary>
public class OandaFillTransaction
{
    /// <summary>
    /// Unique transaction identifier assigned by Oanda.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The instrument that was traded (e.g., "EUR_USD").
    /// </summary>
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// Number of units filled (signed string: positive = bought, negative = sold).
    /// </summary>
    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    /// <summary>
    /// The execution price at which the order was filled.
    /// </summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    /// <summary>
    /// Realized profit/loss from this fill (relevant when closing or reducing a position).
    /// </summary>
    [JsonPropertyName("pl")]
    public string Pl { get; set; } = "0";

    /// <summary>
    /// Commission charged for this transaction.
    /// </summary>
    [JsonPropertyName("commission")]
    public string Commission { get; set; } = "0";

    /// <summary>
    /// Timestamp of the fill in RFC 3339 format.
    /// </summary>
    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}

/// <summary>
/// Transaction details when an order is rejected (cancelled) by Oanda.
/// </summary>
public class OandaCancelTransaction
{
    /// <summary>
    /// Unique transaction identifier assigned by Oanda.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable reason the order was rejected (e.g., "INSUFFICIENT_MARGIN").
    /// </summary>
    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Transaction details when a pending order is successfully created on Oanda.
/// The order is accepted but not yet filled; it will execute when its conditions are met.
/// </summary>
public class OandaCreateTransaction
{
    /// <summary>
    /// Unique order/transaction identifier assigned by Oanda.
    /// Can be used to track, modify, or cancel the pending order.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
