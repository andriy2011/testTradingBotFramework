using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

public class OandaOrderRequest
{
    [JsonPropertyName("order")]
    public OandaOrderBody Order { get; set; } = new();
}

public class OandaOrderBody
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "MARKET";

    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    [JsonPropertyName("price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Price { get; set; }

    [JsonPropertyName("timeInForce")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TimeInForce { get; set; }
}
