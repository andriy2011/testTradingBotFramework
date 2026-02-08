using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PriceMonitoring;

public class PriceUpdateEventArgs : EventArgs
{
    public ExchangeName Exchange { get; init; }
    public string Symbol { get; init; } = string.Empty;
    public decimal Bid { get; init; }
    public decimal Ask { get; init; }
    public decimal Mid => (Bid + Ask) / 2m;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
