using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class AccountBalance
{
    public ExchangeName Exchange { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal TotalBalance { get; set; }
    public decimal AvailableBalance { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
