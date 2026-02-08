// -----------------------------------------------------------------------
// <file>
//   OandaAccountResponse.cs - Oanda v3 API response models for
//   GET /v3/accounts/{id}. Contains account balance, P&L, positions,
//   and pending orders. All numeric values are strings per Oanda API convention.
// </file>
// -----------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

/// <summary>
/// Root response object for the Oanda v3 GET /v3/accounts/{id} endpoint.
/// Wraps the <see cref="OandaAccount"/> in the "account" JSON key.
/// </summary>
public class OandaAccountResponse
{
    /// <summary>
    /// The account details including balance, positions, and open orders.
    /// </summary>
    [JsonPropertyName("account")]
    public OandaAccount Account { get; set; } = new();
}

/// <summary>
/// Oanda account details containing balance, profit/loss metrics,
/// margin information, open positions, and pending orders.
/// </summary>
/// <remarks>
/// All numeric fields are represented as strings, following the Oanda v3 API
/// convention for decimal precision preservation.
/// </remarks>
public class OandaAccount
{
    /// <summary>
    /// Current account balance as a string (e.g., "10000.00").
    /// </summary>
    [JsonPropertyName("balance")]
    public string Balance { get; set; } = "0";

    /// <summary>
    /// Total unrealized profit/loss across all open positions.
    /// </summary>
    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";

    /// <summary>
    /// Total realized profit/loss for the account lifetime.
    /// </summary>
    [JsonPropertyName("pl")]
    public string PL { get; set; } = "0";

    /// <summary>
    /// Available margin for opening new positions.
    /// </summary>
    [JsonPropertyName("marginAvailable")]
    public string MarginAvailable { get; set; } = "0";

    /// <summary>
    /// Base currency of the account (e.g., "USD").
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// List of open positions in the account, each with separate long and short sides.
    /// </summary>
    [JsonPropertyName("positions")]
    public List<OandaPosition> Positions { get; set; } = [];

    /// <summary>
    /// List of pending (unfilled) orders in the account.
    /// </summary>
    [JsonPropertyName("orders")]
    public List<OandaOpenOrder> Orders { get; set; } = [];
}

/// <summary>
/// An open position for a specific instrument on Oanda.
/// Each position has separate <see cref="Long"/> and <see cref="Short"/> sides,
/// as Oanda tracks long and short exposure independently per instrument.
/// </summary>
public class OandaPosition
{
    /// <summary>
    /// The instrument identifier (e.g., "EUR_USD").
    /// </summary>
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// The long side of the position (positive units).
    /// </summary>
    [JsonPropertyName("long")]
    public OandaPositionSide Long { get; set; } = new();

    /// <summary>
    /// The short side of the position (negative units).
    /// </summary>
    [JsonPropertyName("short")]
    public OandaPositionSide Short { get; set; } = new();

    /// <summary>
    /// Combined unrealized P&L for both long and short sides of this position.
    /// </summary>
    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";
}

/// <summary>
/// One side (long or short) of an Oanda position.
/// </summary>
/// <remarks>
/// Units are signed strings: positive for long positions, negative for short positions.
/// A value of "0" indicates no exposure on this side.
/// </remarks>
public class OandaPositionSide
{
    /// <summary>
    /// Number of units held on this side of the position (signed string).
    /// </summary>
    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    /// <summary>
    /// Volume-weighted average entry price for this side of the position.
    /// </summary>
    [JsonPropertyName("averagePrice")]
    public string AveragePrice { get; set; } = "0";

    /// <summary>
    /// Unrealized P&L for this side of the position.
    /// </summary>
    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";
}

/// <summary>
/// A pending (unfilled) order on the Oanda account.
/// Represents limit, stop, and other non-market order types awaiting execution.
/// </summary>
public class OandaOpenOrder
{
    /// <summary>
    /// Unique order identifier assigned by Oanda.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Order type string (e.g., "LIMIT", "STOP", "MARKET_IF_TOUCHED").
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// The instrument this order targets (e.g., "EUR_USD").
    /// </summary>
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    /// <summary>
    /// Number of units to trade (signed string: positive = buy, negative = sell).
    /// </summary>
    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    /// <summary>
    /// The trigger or limit price for the order.
    /// </summary>
    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";
}
