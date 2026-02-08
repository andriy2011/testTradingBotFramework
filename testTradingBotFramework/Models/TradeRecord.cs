using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class TradeRecord
{
    public string TradeId { get; set; } = Guid.NewGuid().ToString("N");
    public string OrderId { get; set; } = string.Empty;
    public string? ExchangeOrderId { get; set; }
    public string? SignalId { get; set; }
    public ExchangeName Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public string FeeAsset { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
