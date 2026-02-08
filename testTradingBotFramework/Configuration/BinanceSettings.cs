namespace testTradingBotFramework.Configuration;

public class BinanceSettings
{
    public const string SectionName = "Binance";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public bool UseTestnet { get; set; } = true;
}
