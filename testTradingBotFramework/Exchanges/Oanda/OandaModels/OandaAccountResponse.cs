using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

public class OandaAccountResponse
{
    [JsonPropertyName("account")]
    public OandaAccount Account { get; set; } = new();
}

public class OandaAccount
{
    [JsonPropertyName("balance")]
    public string Balance { get; set; } = "0";

    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";

    [JsonPropertyName("pl")]
    public string PL { get; set; } = "0";

    [JsonPropertyName("marginAvailable")]
    public string MarginAvailable { get; set; } = "0";

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";

    [JsonPropertyName("positions")]
    public List<OandaPosition> Positions { get; set; } = [];

    [JsonPropertyName("orders")]
    public List<OandaOpenOrder> Orders { get; set; } = [];
}

public class OandaPosition
{
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("long")]
    public OandaPositionSide Long { get; set; } = new();

    [JsonPropertyName("short")]
    public OandaPositionSide Short { get; set; } = new();

    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";
}

public class OandaPositionSide
{
    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    [JsonPropertyName("averagePrice")]
    public string AveragePrice { get; set; } = "0";

    [JsonPropertyName("unrealizedPL")]
    public string UnrealizedPL { get; set; } = "0";
}

public class OandaOpenOrder
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";
}
