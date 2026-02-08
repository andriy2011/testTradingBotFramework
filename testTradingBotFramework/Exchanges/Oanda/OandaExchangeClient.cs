using Microsoft.Extensions.Logging;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Oanda;

public class OandaExchangeClient : IExchangeClient
{
    private readonly OandaApiClient _api;
    private readonly ILogger<OandaExchangeClient> _logger;

    public OandaExchangeClient(OandaApiClient api, ILogger<OandaExchangeClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        var request = OandaOrderMapper.ToOandaOrder(order);

        _logger.LogInformation("Placing Oanda order: {Instrument} {Units} {Type}",
            request.Order.Instrument, request.Order.Units, request.Order.Type);

        var response = await _api.PlaceOrderAsync(request, ct);

        if (response?.OrderFillTransaction is not null)
        {
            var fill = response.OrderFillTransaction;
            return OrderResult.Succeeded(
                exchangeOrderId: fill.Id,
                status: Models.Enums.OrderStatus.Filled,
                filledQty: Math.Abs(decimal.Parse(fill.Units)),
                avgPrice: decimal.Parse(fill.Price),
                fee: Math.Abs(decimal.TryParse(fill.Commission, out var c) ? c : 0),
                feeAsset: "USD");
        }

        if (response?.OrderCancelTransaction is not null)
        {
            return OrderResult.Failed($"Order cancelled: {response.OrderCancelTransaction.Reason}");
        }

        if (response?.OrderCreateTransaction is not null)
        {
            return OrderResult.Succeeded(
                response.OrderCreateTransaction.Id,
                Models.Enums.OrderStatus.Submitted,
                0, null);
        }

        return OrderResult.Failed("Unexpected Oanda response");
    }

    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        var success = await _api.CancelOrderAsync(exchangeOrderId, ct);
        return success
            ? OrderResult.Succeeded(exchangeOrderId, Models.Enums.OrderStatus.Cancelled, 0, null)
            : OrderResult.Failed("Failed to cancel Oanda order");
    }

    public Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default)
    {
        _logger.LogWarning("Oanda order modification not yet implemented. Cancel and replace instead.");
        return Task.FromResult(OrderResult.Failed("Modify not supported for Oanda. Use cancel + replace."));
    }

    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null) return Array.Empty<Order>();

        var orders = account.Account.Orders
            .Select(OandaOrderMapper.ToLocalOrder)
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(symbol))
            orders = orders.Where(o => o.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        return orders.ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null) return Array.Empty<Position>();

        return account.Account.Positions
            .SelectMany(OandaPositionMapper.ToLocalPositions)
            .ToList()
            .AsReadOnly();
    }

    public async Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null)
            return new AccountBalance { Exchange = ExchangeName.Oanda };

        return new AccountBalance
        {
            Exchange = ExchangeName.Oanda,
            Currency = account.Account.Currency,
            TotalBalance = decimal.Parse(account.Account.Balance),
            AvailableBalance = decimal.Parse(account.Account.MarginAvailable),
            UnrealizedPnL = decimal.Parse(account.Account.UnrealizedPL)
        };
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default)
    {
        var pricing = await _api.GetPricingAsync(symbol, ct);
        if (pricing is null || pricing.Prices.Count == 0)
            return 0;

        var price = pricing.Prices[0];
        var bid = decimal.Parse(price.Bids[0].Price);
        var ask = decimal.Parse(price.Asks[0].Price);
        return (bid + ask) / 2m;
    }
}
