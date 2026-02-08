using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using BinanceEnums = Binance.Net.Enums;

namespace testTradingBotFramework.Exchanges.Binance;

public class BinanceExchangeClient : IExchangeClient
{
    private readonly IBinanceRestClient _restClient;
    private readonly ILogger<BinanceExchangeClient> _logger;

    public BinanceExchangeClient(
        IBinanceRestClient restClient,
        ILogger<BinanceExchangeClient> logger)
    {
        _restClient = restClient;
        _logger = logger;
    }

    public async Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        var side = BinanceOrderMapper.ToOrderSide(order.Side);
        var type = BinanceOrderMapper.ToFuturesOrderType(order.OrderType);

        _logger.LogInformation("Placing Binance futures order: {Symbol} {Side} {Type} Qty={Qty} Price={Price}",
            order.Symbol, side, type, order.Quantity, order.Price);

        var result = await _restClient.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: order.Symbol,
            side: side,
            type: type,
            quantity: order.Quantity,
            price: order.OrderType == Models.Enums.OrderType.Limit || order.OrderType == Models.Enums.OrderType.StopLimit
                ? order.Price
                : null,
            timeInForce: type == BinanceEnums.FuturesOrderType.Limit ? BinanceEnums.TimeInForce.GoodTillCanceled : null,
            ct: ct);

        if (!result.Success)
        {
            _logger.LogError("Binance order failed: {Error}", result.Error?.Message);
            return OrderResult.Failed(result.Error?.Message ?? "Unknown Binance error");
        }

        var data = result.Data;
        return OrderResult.Succeeded(
            exchangeOrderId: data.Id.ToString(),
            status: BinanceOrderMapper.ToLocalOrderStatus(data.Status),
            filledQty: data.QuantityFilled,
            avgPrice: data.AveragePrice > 0 ? data.AveragePrice : null);
    }

    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Trading.CancelOrderAsync(
            symbol, long.Parse(exchangeOrderId), ct: ct);

        if (!result.Success)
            return OrderResult.Failed(result.Error?.Message ?? "Cancel failed");

        return OrderResult.Succeeded(exchangeOrderId, Models.Enums.OrderStatus.Cancelled, 0, null);
    }

    public async Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Trading.EditOrderAsync(
            symbol: updatedOrder.Symbol,
            side: BinanceOrderMapper.ToOrderSide(updatedOrder.Side),
            quantity: updatedOrder.Quantity,
            price: updatedOrder.Price ?? 0,
            orderId: long.Parse(exchangeOrderId),
            ct: ct);

        if (!result.Success)
            return OrderResult.Failed(result.Error?.Message ?? "Modify failed");

        var data = result.Data;
        return OrderResult.Succeeded(
            data.Id.ToString(),
            BinanceOrderMapper.ToLocalOrderStatus(data.Status),
            data.QuantityFilled,
            data.AveragePrice > 0 ? data.AveragePrice : null);
    }

    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Trading.GetOpenOrdersAsync(symbol, ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get open orders: {Error}", result.Error?.Message);
            return Array.Empty<Order>();
        }

        return result.Data.Select(o => new Order
        {
            ExchangeOrderId = o.Id.ToString(),
            Exchange = ExchangeName.Binance,
            Symbol = o.Symbol,
            Side = o.Side == BinanceEnums.OrderSide.Buy ? Models.Enums.OrderSide.Buy : Models.Enums.OrderSide.Sell,
            OrderType = Models.Enums.OrderType.Market,
            Quantity = o.Quantity,
            Price = o.Price,
            Status = BinanceOrderMapper.ToLocalOrderStatus(o.Status),
            FilledQuantity = o.QuantityFilled,
            AverageFillPrice = o.AveragePrice > 0 ? o.AveragePrice : null,
            CreatedAt = o.CreateTime
        }).ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Account.GetPositionInformationAsync(ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get positions: {Error}", result.Error?.Message);
            return Array.Empty<Position>();
        }

        return result.Data
            .Where(p => p.Quantity != 0)
            .Select(BinancePositionMapper.ToLocalPosition)
            .ToList()
            .AsReadOnly();
    }

    public async Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Account.GetAccountInfoV3Async(ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get account balance: {Error}", result.Error?.Message);
            return new AccountBalance { Exchange = ExchangeName.Binance };
        }

        return new AccountBalance
        {
            Exchange = ExchangeName.Binance,
            Currency = "USDT",
            TotalBalance = result.Data.TotalWalletBalance,
            AvailableBalance = result.Data.AvailableBalance,
            UnrealizedPnL = result.Data.TotalUnrealizedProfit
        };
    }

    public async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.ExchangeData.GetPriceAsync(symbol, ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get price for {Symbol}: {Error}", symbol, result.Error?.Message);
            return 0;
        }
        return result.Data.Price;
    }
}
