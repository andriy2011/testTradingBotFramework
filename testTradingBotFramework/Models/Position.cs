using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class Position
{
    public string PositionId { get; set; } = Guid.NewGuid().ToString("N");
    public ExchangeName Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public PositionSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal EntryPrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public DateTimeOffset OpenedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUpdatedAt { get; set; }

    public void UpdateCurrentPrice(decimal price)
    {
        CurrentPrice = price;
        UnrealizedPnL = Side == PositionSide.Long
            ? (price - EntryPrice) * Quantity
            : (EntryPrice - price) * Quantity;
        LastUpdatedAt = DateTimeOffset.UtcNow;
    }
}
