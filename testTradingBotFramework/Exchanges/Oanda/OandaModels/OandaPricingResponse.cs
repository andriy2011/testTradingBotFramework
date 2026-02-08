using System.Text.Json.Serialization;

namespace testTradingBotFramework.Exchanges.Oanda.OandaModels;

public class OandaPricingResponse
{
    [JsonPropertyName("prices")]
    public List<OandaPrice> Prices { get; set; } = [];
}

public class OandaPrice
{
    [JsonPropertyName("instrument")]
    public string Instrument { get; set; } = string.Empty;

    [JsonPropertyName("asks")]
    public List<OandaPriceLevel> Asks { get; set; } = [];

    [JsonPropertyName("bids")]
    public List<OandaPriceLevel> Bids { get; set; } = [];

    [JsonPropertyName("time")]
    public string Time { get; set; } = string.Empty;
}

public class OandaPriceLevel
{
    [JsonPropertyName("price")]
    public string Price { get; set; } = "0";

    [JsonPropertyName("liquidity")]
    public int Liquidity { get; set; }
}

public class OandaPricingStreamLine
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("instrument")]
    public string? Instrument { get; set; }

    [JsonPropertyName("asks")]
    public List<OandaPriceLevel>? Asks { get; set; }

    [JsonPropertyName("bids")]
    public List<OandaPriceLevel>? Bids { get; set; }

    [JsonPropertyName("time")]
    public string? Time { get; set; }
}
