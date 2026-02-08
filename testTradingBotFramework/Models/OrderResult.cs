using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

public class OrderResult
{
    public bool Success { get; set; }
    public string? ExchangeOrderId { get; set; }
    public OrderStatus Status { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? AverageFillPrice { get; set; }
    public decimal? Fee { get; set; }
    public string? FeeAsset { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public static OrderResult Succeeded(string exchangeOrderId, OrderStatus status, decimal filledQty, decimal? avgPrice, decimal? fee = null, string? feeAsset = null) =>
        new()
        {
            Success = true,
            ExchangeOrderId = exchangeOrderId,
            Status = status,
            FilledQuantity = filledQty,
            AverageFillPrice = avgPrice,
            Fee = fee,
            FeeAsset = feeAsset
        };

    public static OrderResult Failed(string error) =>
        new()
        {
            Success = false,
            ErrorMessage = error,
            Status = OrderStatus.Rejected
        };
}
