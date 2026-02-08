using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

public class OandaOrderResponse
{
    [JsonPropertyName("orderFillTransaction")]
    public OandaFillTransaction? OrderFillTransaction { get; set; }

    [JsonPropertyName("orderCancelTransaction")]
    public OandaCancelTransaction? OrderCancelTransaction { get; set; }

    [JsonPropertyName("orderCreateTransaction")]
    public OandaCreateTransaction? OrderCreateTransaction { get; set; }
}

public class OandaFillTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("units")]
    public string Units { get; set; } = "0";

    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("pl")]
    public string Pl { get; set; } = "0";

    [JsonPropertyName("commission")]
    public string Commission { get; set; } = "0";

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}

public class OandaCancelTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class OandaCreateTransaction
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
