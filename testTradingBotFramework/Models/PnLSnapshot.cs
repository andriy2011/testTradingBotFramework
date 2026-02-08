using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class PnLSnapshot
{
    public ExchangeName Exchange { get; set; }
    public decimal RealizedPnL { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal TotalFees { get; set; }
    public decimal NetPnL => RealizedPnL + UnrealizedPnL - TotalFees;
    public int TotalTrades { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
