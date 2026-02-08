// ============================================================================
// BinanceExchangeClient.cs
//
// Binance USD-M Futures exchange client implementation. This class wraps the
// Binance.Net SDK (IBinanceRestClient) and implements IExchangeClient to
// provide a unified interface for the trading bot framework.
//
// Key design patterns:
//   - Request-Check Pattern: Every API call follows the same flow:
//     1. Call the Binance.Net SDK method
//     2. Check the result.Success flag
//     3. On failure: log the error and return a failure result
//     4. On success: map the Binance-specific types to local domain models
//
//   - Type Mapping: Uses BinanceOrderMapper for order-related conversions
//     (side, order type, status) and BinancePositionMapper for position
//     conversions. This keeps Binance-specific enums isolated from the
//     rest of the application.
//
//   - All operations target the USD-M Futures API (UsdFuturesApi), not
//     the Spot API, since the framework is designed for futures trading.
//
// Dependencies:
//   - IBinanceRestClient (from Binance.Net SDK) -- configured with API
//     credentials during DI registration
//   - ILogger<BinanceExchangeClient> -- structured logging for diagnostics
// ============================================================================

using Binance.Net.Interfaces.Clients;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using BinanceEnums = Binance.Net.Enums;

namespace testTradingBotFramework.Exchanges.Binance;

/// <summary>
/// Binance USD-M Futures implementation of <see cref="IExchangeClient"/>.
/// Wraps the Binance.Net SDK to provide order management, position tracking,
/// account balance retrieval, and market data access for Binance Futures.
/// </summary>
public class BinanceExchangeClient : IExchangeClient
{
    /// <summary>The Binance.Net REST client used for all API calls.</summary>
    private readonly IBinanceRestClient _restClient;

    /// <summary>Logger for recording API call outcomes and diagnostics.</summary>
    private readonly ILogger<BinanceExchangeClient> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="BinanceExchangeClient"/>.
    /// </summary>
    /// <param name="restClient">
    /// The Binance.Net REST client, pre-configured with API key and secret.
    /// </param>
    /// <param name="logger">Logger for structured diagnostic output.</param>
    public BinanceExchangeClient(
        IBinanceRestClient restClient,
        ILogger<BinanceExchangeClient> logger)
    {
        _restClient = restClient;
        _logger = logger;
    }

    /// <summary>
    /// Places a new order on Binance USD-M Futures.
    /// Maps the local order model to Binance-specific types, sends the request,
    /// and converts the response back to a local <see cref="OrderResult"/>.
    /// </summary>
    /// <param name="order">The order to place, containing symbol, side, type, quantity, and optional price.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> containing the exchange order ID, mapped status,
    /// filled quantity, and average fill price on success; or an error message on failure.
    /// </returns>
    public async Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        // Map local enums to Binance SDK enums via the dedicated mapper
        var side = BinanceOrderMapper.ToOrderSide(order.Side);
        var type = BinanceOrderMapper.ToFuturesOrderType(order.OrderType);

        _logger.LogInformation("Placing Binance futures order: {Symbol} {Side} {Type} Qty={Qty} Price={Price}",
            order.Symbol, side, type, order.Quantity, order.Price);

        var result = await _restClient.UsdFuturesApi.Trading.PlaceOrderAsync(
            symbol: order.Symbol,
            side: side,
            type: type,
            quantity: order.Quantity,
            // Price is only sent for Limit and StopLimit orders; Market orders use null
            // to let Binance fill at the current market price.
            price: order.OrderType == Models.Enums.OrderType.Limit || order.OrderType == Models.Enums.OrderType.StopLimit
                ? order.Price
                : null,
            // TimeInForce is required for Limit orders on Binance Futures.
            // GoodTillCanceled (GTC) keeps the order open until filled or manually cancelled.
            // For non-Limit types (Market, Stop, etc.), TimeInForce must be null.
            timeInForce: type == BinanceEnums.FuturesOrderType.Limit ? BinanceEnums.TimeInForce.GoodTillCanceled : null,
            ct: ct);

        if (!result.Success)
        {
            _logger.LogError("Binance order failed: {Error}", result.Error?.Message);
            return OrderResult.Failed(result.Error?.Message ?? "Unknown Binance error");
        }

        // Map the Binance response to our local OrderResult, converting the
        // Binance order status enum and handling the case where AveragePrice
        // is zero (order not yet filled).
        var data = result.Data;
        return OrderResult.Succeeded(
            exchangeOrderId: data.Id.ToString(),
            status: BinanceOrderMapper.ToLocalOrderStatus(data.Status),
            filledQty: data.QuantityFilled,
            avgPrice: data.AveragePrice > 0 ? data.AveragePrice : null);
    }

    /// <summary>
    /// Cancels an existing open order on Binance USD-M Futures.
    /// </summary>
    /// <param name="exchangeOrderId">
    /// The Binance-assigned order ID (stored as string, parsed to long for the API).
    /// </param>
    /// <param name="symbol">The trading symbol (required by Binance's cancel API).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> with Cancelled status on success, or an error message on failure.
    /// </returns>
    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        // Binance requires the order ID as a long integer, so parse the string ID
        var result = await _restClient.UsdFuturesApi.Trading.CancelOrderAsync(
            symbol, long.Parse(exchangeOrderId), ct: ct);

        if (!result.Success)
            return OrderResult.Failed(result.Error?.Message ?? "Cancel failed");

        // On successful cancellation, return Cancelled status with zero fill
        return OrderResult.Succeeded(exchangeOrderId, Models.Enums.OrderStatus.Cancelled, 0, null);
    }

    /// <summary>
    /// Modifies an existing open order on Binance USD-M Futures by editing its
    /// quantity and/or price in-place (Binance's native order edit endpoint).
    /// </summary>
    /// <param name="exchangeOrderId">
    /// The Binance-assigned order ID to modify (parsed to long for the API).
    /// </param>
    /// <param name="updatedOrder">The new order parameters to apply.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> reflecting the updated order state from Binance.
    /// </returns>
    public async Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Trading.EditOrderAsync(
            symbol: updatedOrder.Symbol,
            side: BinanceOrderMapper.ToOrderSide(updatedOrder.Side),
            quantity: updatedOrder.Quantity,
            // Binance EditOrder requires a non-null price; default to 0 if not set
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

    /// <summary>
    /// Retrieves all currently open orders on Binance USD-M Futures,
    /// optionally filtered by symbol. Maps each Binance order to the local
    /// <see cref="Order"/> domain model.
    /// </summary>
    /// <param name="symbol">Optional symbol filter; null returns orders for all symbols.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A read-only list of open orders mapped to the local model.
    /// Returns an empty list on API failure.
    /// </returns>
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Trading.GetOpenOrdersAsync(symbol, ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get open orders: {Error}", result.Error?.Message);
            return Array.Empty<Order>();
        }

        // Map each Binance order DTO to our local Order model.
        // Note: OrderType is defaulted to Market here as a simplification;
        // the exact Binance order type mapping could be extended if needed.
        return result.Data.Select(o => new Order
        {
            ExchangeOrderId = o.Id.ToString(),
            Exchange = ExchangeName.Binance,
            Symbol = o.Symbol,
            // Map Binance OrderSide enum to local OrderSide enum
            Side = o.Side == BinanceEnums.OrderSide.Buy ? Models.Enums.OrderSide.Buy : Models.Enums.OrderSide.Sell,
            OrderType = Models.Enums.OrderType.Market,
            Quantity = o.Quantity,
            Price = o.Price,
            Status = BinanceOrderMapper.ToLocalOrderStatus(o.Status),
            FilledQuantity = o.QuantityFilled,
            // AveragePrice of 0 means no fills yet; store as null for clarity
            AverageFillPrice = o.AveragePrice > 0 ? o.AveragePrice : null,
            CreatedAt = o.CreateTime
        }).ToList().AsReadOnly();
    }

    /// <summary>
    /// Retrieves all open (non-zero quantity) positions on Binance USD-M Futures.
    /// Uses <see cref="BinancePositionMapper"/> to convert Binance position data
    /// to the local <see cref="Position"/> model.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A read-only list of active positions. Returns an empty list on API failure.
    /// </returns>
    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        var result = await _restClient.UsdFuturesApi.Account.GetPositionInformationAsync(ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get positions: {Error}", result.Error?.Message);
            return Array.Empty<Position>();
        }

        // Binance returns position entries for ALL symbols, including those with
        // zero quantity (no open position). Filter to only non-zero quantities
        // to get actually open positions, then map to local model.
        return result.Data
            .Where(p => p.Quantity != 0)
            .Select(BinancePositionMapper.ToLocalPosition)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Retrieves the current account balance for the Binance USD-M Futures account.
    /// Returns total wallet balance, available balance, and unrealized PnL in USDT.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="AccountBalance"/> populated with USDT balances.
    /// Returns a default (zero-value) balance on API failure.
    /// </returns>
    public async Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        // Use the V3 account info endpoint for USD-M Futures
        var result = await _restClient.UsdFuturesApi.Account.GetAccountInfoV3Async(ct: ct);
        if (!result.Success)
        {
            _logger.LogError("Failed to get account balance: {Error}", result.Error?.Message);
            // Return a default balance object so callers always get a non-null result
            return new AccountBalance { Exchange = ExchangeName.Binance };
        }

        // Binance USD-M Futures balances are denominated in USDT
        return new AccountBalance
        {
            Exchange = ExchangeName.Binance,
            Currency = "USDT",
            TotalBalance = result.Data.TotalWalletBalance,
            AvailableBalance = result.Data.AvailableBalance,
            UnrealizedPnL = result.Data.TotalUnrealizedProfit
        };
    }

    /// <summary>
    /// Fetches the current market price for a symbol on Binance USD-M Futures.
    /// </summary>
    /// <param name="symbol">The Binance futures symbol (e.g., "BTCUSDT", "ETHUSDT").</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// The current price as a decimal. Returns 0 if the API call fails.
    /// </returns>
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
