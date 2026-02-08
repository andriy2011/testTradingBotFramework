using testTradingBotFramework.Models;

namespace testTradingBotFramework.Exchanges;

public interface IExchangeClient
{
    Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default);
    Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default);
    Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default);
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default);
    Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default);
    Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default);
}
