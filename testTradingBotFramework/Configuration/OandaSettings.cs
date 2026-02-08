namespace testTradingBotFramework.Configuration;

public class OandaSettings
{
    public const string SectionName = "Oanda";

    public string ApiToken { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public bool UsePractice { get; set; } = true;

    public string RestBaseUrl => UsePractice
        ? "https://api-fxpractice.oanda.com"
        : "https://api-fxtrade.oanda.com";

    public string StreamBaseUrl => UsePractice
        ? "https://stream-fxpractice.oanda.com"
        : "https://stream-fxtrade.oanda.com";
}
