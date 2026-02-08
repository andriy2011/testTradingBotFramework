// ============================================================================
// IExchangeClient.cs
//
// Core exchange abstraction for the trading bot framework. All exchange
// interactions (order management, position queries, balance retrieval, and
// market data) flow through this interface, ensuring that strategies and
// services remain decoupled from any specific exchange implementation.
//
// Concrete implementations:
//   - BinanceExchangeClient  (Binance USD-M crypto futures)
//   - OandaExchangeClient    (OANDA forex trading)
//
// Resolution: Implementations are registered as keyed services in the DI
// container using the ExchangeName enum as the key. The ExchangeFactory
// resolves the correct client at runtime.
//
// Each method returns rich result types (OrderResult, Order, Position,
// AccountBalance) that normalize exchange-specific responses into a
// unified domain model.
// ============================================================================

using testTradingBotFramework.Models;

namespace testTradingBotFramework.Exchanges;

/// <summary>
/// Defines the contract for all exchange client implementations.
/// Provides a unified API for placing/cancelling/modifying orders,
/// querying open orders and positions, retrieving account balances,
/// and fetching current market prices.
/// </summary>
public interface IExchangeClient
{
    /// <summary>
    /// Submits a new order to the exchange.
    /// </summary>
    /// <param name="order">
    /// The order details including symbol, side, type, quantity, and optional price.
    /// </param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> indicating success or failure, along with
    /// the exchange-assigned order ID, fill status, filled quantity, and average price.
    /// </returns>
    Task<OrderResult> PlaceOrderAsync(Order order, CancellationToken ct = default);

    /// <summary>
    /// Cancels an existing open order on the exchange.
    /// </summary>
    /// <param name="exchangeOrderId">The exchange-assigned order identifier.</param>
    /// <param name="symbol">The trading symbol (required by most exchange APIs for order lookup).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> confirming cancellation or describing the failure reason.
    /// </returns>
    Task<OrderResult> CancelOrderAsync(string exchangeOrderId, string symbol, CancellationToken ct = default);

    /// <summary>
    /// Modifies an existing open order (e.g., changes price or quantity).
    /// Not all exchanges support native order modification; some may cancel and re-place.
    /// </summary>
    /// <param name="exchangeOrderId">The exchange-assigned order identifier to modify.</param>
    /// <param name="updatedOrder">The new order parameters (symbol, side, quantity, price).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="OrderResult"/> with the updated order state from the exchange.
    /// </returns>
    Task<OrderResult> ModifyOrderAsync(string exchangeOrderId, Order updatedOrder, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all currently open (unfilled or partially filled) orders.
    /// </summary>
    /// <param name="symbol">
    /// Optional symbol filter. When null, returns open orders across all symbols.
    /// </param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A read-only list of <see cref="Order"/> objects mapped to the local domain model.
    /// Returns an empty list if the API call fails.
    /// </returns>
    Task<IReadOnlyList<Order>> GetOpenOrdersAsync(string? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all currently open positions (non-zero quantity).
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// A read-only list of <see cref="Position"/> objects representing active holdings.
    /// Returns an empty list if the API call fails.
    /// </returns>
    Task<IReadOnlyList<Position>> GetOpenPositionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current account balance, including total balance,
    /// available (free) balance, and unrealized profit/loss.
    /// </summary>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// An <see cref="AccountBalance"/> snapshot. Returns a default balance object
    /// (with zero values) if the API call fails.
    /// </returns>
    Task<AccountBalance> GetAccountBalanceAsync(CancellationToken ct = default);

    /// <summary>
    /// Fetches the current market price for a given trading symbol.
    /// </summary>
    /// <param name="symbol">The trading pair symbol (e.g., "BTCUSDT" for Binance, "EUR_USD" for OANDA).</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>
    /// The current price as a decimal. Returns 0 if the API call fails.
    /// </returns>
    Task<decimal> GetCurrentPriceAsync(string symbol, CancellationToken ct = default);
}
