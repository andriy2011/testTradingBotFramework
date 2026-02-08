// -----------------------------------------------------------------------
// OandaExchangeClient.cs
//
// IExchangeClient implementation for the Oanda forex broker.
// Delegates all HTTP communication to OandaApiClient and translates
// between the framework's domain models (Order, Position, AccountBalance)
// and Oanda-specific API models.
//
// Key behaviors:
//   - PlaceOrderAsync handles three Oanda response scenarios:
//       1. OrderFillTransaction  -- market order filled immediately
//       2. OrderCancelTransaction -- order rejected by the broker
//       3. OrderCreateTransaction -- pending/limit order accepted
//   - GetCurrentPriceAsync returns the mid-price (average of best bid/ask).
//   - ModifyOrderAsync is not supported; Oanda requires cancel-and-replace.
//   - All Oanda numeric values arrive as strings and are parsed to decimal.
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using testTradingBotFramework.Exchanges.Oanda.OandaModels;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Oanda;

/// <summary>
/// Oanda-specific implementation of <see cref="IExchangeClient"/>.
/// Translates framework-level trading operations into Oanda API calls
/// and maps Oanda responses back into the framework's unified domain models.
/// </summary>
public class OandaExchangeClient : IExchangeClient
{
    /// <summary>Low-level Oanda HTTP client for REST API communication.</summary>
    private readonly OandaApiClient _api;

    /// <summary>Structured logger for order lifecycle events and diagnostics.</summary>
    private readonly ILogger<OandaExchangeClient> _logger;

    /// <summary>
    /// Initializes the exchange client with the Oanda API client and logger.
    /// </summary>
    /// <param name="api">The low-level Oanda API client that handles HTTP transport.</param>
    /// <param name="logger">Logger for recording order events and warnings.</param>
    public OandaExchangeClient(OandaApiClient api, ILogger<OandaExchangeClient> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Places an order on Oanda by converting the framework Order to an Oanda-specific
    /// request and interpreting the three possible response scenarios.
    /// </summary>
    /// <param name="order">The framework-level order to place.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> indicating success (with fill details or submission
    /// confirmation) or failure (with the rejection reason).
    /// </returns>
    public async Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default)
    {
        // Convert framework Order to Oanda-specific request format
        var request = OandaOrderMapper.ToOandaOrder(order);

        _logger.LogInformation("Placing Oanda order: {Instrument} {Units} {Type}",
            request.Order.Instrument, request.Order.Units, request.Order.Type);

        var response = await _api.PlaceOrderAsync(request, ct);

        // Scenario 1: Market order was filled immediately.
        // Extract fill price, quantity, and commission from the fill transaction.
        // Units are absolute-valued because Oanda uses negative units for sell orders.
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

        // Scenario 2: Order was rejected/cancelled by the broker (e.g., insufficient margin).
        if (response?.OrderCancelTransaction is not null)
        {
            return OrderResult.Failed($"Order cancelled: {response.OrderCancelTransaction.Reason}");
        }

        // Scenario 3: Pending/limit order was accepted but not yet filled.
        // Returns Submitted status with no fill quantity or price.
        if (response?.OrderCreateTransaction is not null)
        {
            return OrderResult.Succeeded(
                response.OrderCreateTransaction.Id,
                Models.Enums.OrderStatus.Submitted,
                0, null);
        }

        // Fallback: response was null or contained no recognized transaction type
        return OrderResult.Failed("Unexpected Oanda response");
    }

    /// <summary>
    /// Cancels an existing pending order on Oanda by its exchange-assigned order ID.
    /// </summary>
    /// <param name="exchangeOrderId">The Oanda-assigned order identifier.</param>
    /// <param name="symbol">The instrument symbol (not used by Oanda cancel API, but required by interface).</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Success result with Cancelled status, or failure if the API call failed.</returns>
    public async Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default)
    {
        var success = await _api.CancelOrderAsync(exchangeOrderId, ct);
        return success
            ? OrderResult.Succeeded(exchangeOrderId, Models.Enums.OrderStatus.Cancelled, 0, null)
            : OrderResult.Failed("Failed to cancel Oanda order");
    }

    /// <summary>
    /// Order modification is not supported by this Oanda implementation.
    /// Oanda's v3 API does not provide a direct modify endpoint for most order types;
    /// the recommended approach is to cancel the existing order and place a new one.
    /// </summary>
    /// <param name="exchangeOrderId">The order ID (unused).</param>
    /// <param name="updatedOrder">The updated order details (unused).</param>
    /// <param name="ct">Cancellation token (unused).</param>
    /// <returns>Always returns a failed OrderResult advising cancel + replace.</returns>
    public Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default)
    {
        _logger.LogWarning("Oanda order modification not yet implemented. Cancel and replace instead.");
        return Task.FromResult(OrderResult.Failed("Modify not supported for Oanda. Use cancel + replace."));
    }

    /// <summary>
    /// Retrieves all open (pending) orders from the Oanda account, optionally filtered by symbol.
    /// Uses the account endpoint which includes open orders in its response payload.
    /// </summary>
    /// <param name="symbol">Optional instrument symbol to filter by (case-insensitive). Null returns all.</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Read-only list of framework Order objects, or empty list on API failure.</returns>
    public async Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null) return Array.Empty<Order>();

        // Map each Oanda order to the framework's unified Order model
        var orders = account.Account.Orders
            .Select(OandaOrderMapper.ToLocalOrder)
            .AsEnumerable();

        // Apply optional client-side symbol filter (case-insensitive comparison)
        if (!string.IsNullOrWhiteSpace(symbol))
            orders = orders.Where(o => o.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        return orders.ToList().AsReadOnly();
    }

    /// <summary>
    /// Retrieves all open positions from the Oanda account.
    /// Oanda reports positions per instrument with separate long/short sides,
    /// so each Oanda position may produce 0, 1, or 2 framework Position objects
    /// via <see cref="OandaPositionMapper.ToLocalPositions"/>.
    /// </summary>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Read-only list of framework Position objects, or empty list on API failure.</returns>
    public async Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null) return Array.Empty<Position>();

        // SelectMany flattens the 0-2 positions per instrument into a single list
        return account.Account.Positions
            .SelectMany(OandaPositionMapper.ToLocalPositions)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Retrieves the current account balance, available margin, and unrealized P&amp;L.
    /// All numeric values from Oanda arrive as strings and are parsed to decimal.
    /// </summary>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>
    /// Populated AccountBalance on success, or a default AccountBalance
    /// (with only Exchange set) if the API call fails.
    /// </returns>
    public async Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default)
    {
        var account = await _api.GetAccountAsync(ct);
        if (account is null)
            // Return a minimal AccountBalance so callers always get a non-null object
            return new AccountBalance { Exchange = ExchangeName.Oanda };

        // Parse Oanda's string-encoded numeric fields into decimal values
        return new AccountBalance
        {
            Exchange = ExchangeName.Oanda,
            Currency = account.Account.Currency,
            TotalBalance = decimal.Parse(account.Account.Balance),
            AvailableBalance = decimal.Parse(account.Account.MarginAvailable),
            UnrealizedPnL = decimal.Parse(account.Account.UnrealizedPL)
        };
    }

    /// <summary>
    /// Gets the current mid-price for a single instrument by averaging the best bid and ask.
    /// The mid-price provides a fair value estimate that is equidistant from both sides
    /// of the order book.
    /// </summary>
    /// <param name="symbol">The Oanda instrument name (e.g., "EUR_USD").</param>
    /// <param name="ct">Cancellation token for async operation.</param>
    /// <returns>Mid-price as (bestBid + bestAsk) / 2, or 0 if pricing is unavailable.</returns>
    public async Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default)
    {
        var pricing = await _api.GetPricingAsync(symbol, ct);
        if (pricing is null || pricing.Prices.Count == 0)
            return 0;

        // Take the first (and typically only) price entry for the requested instrument
        var price = pricing.Prices[0];
        var bid = decimal.Parse(price.Bids[0].Price);
        var ask = decimal.Parse(price.Asks[0].Price);
        // Calculate mid-price: the arithmetic mean of best bid and best ask
        return (bid + ask) / 2m;
    }
}
