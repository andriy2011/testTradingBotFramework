using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class TradeSignal
{
    public string SignalId { get; set; } = Guid.NewGuid().ToString("N");
    public ExchangeName Exchange { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public SignalAction Action { get; set; }
    public OrderSide Side { get; set; }
    public OrderType OrderType { get; set; } = OrderType.Market;
    public AssetClass AssetClass { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? Price { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, string> Metadata { get; set; } = new();
}
