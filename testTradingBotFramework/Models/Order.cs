using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class Order
{
    public string OrderId { get; set; } = Guid.NewGuid().ToString("N");
    public string? ExchangeOrderId { get; set; }
    public ExchangeName Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public OrderSide Side { get; set; }
    public OrderType OrderType { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal FilledQuantity { get; set; }
    public decimal? AverageFillPrice { get; set; }
    public string? SignalId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
