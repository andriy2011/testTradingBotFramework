// -----------------------------------------------------------------------
// OrderResult.cs
//
// Result type returned by IExchangeClient after an order operation
// (place, cancel, etc.). Uses the static factory pattern with
// Succeeded() and Failed() methods for clean, intention-revealing
// construction. Fee and FeeAsset track the trading costs charged by
// the exchange (e.g., fee = 0.001, feeAsset = "BNB").
// Failed results automatically set the status to Rejected.
// -----------------------------------------------------------------------

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Models;

/// <summary>
/// Encapsulates the outcome of an order operation performed by an exchange client.
/// <para>
/// Prefer using the static factory methods <see cref="Succeeded"/> and <see cref="Failed"/>
/// rather than constructing instances directly, as they ensure consistent initialization
/// of related properties (e.g., Failed always sets Status to Rejected).
/// </para>
/// </summary>
public class OrderResult
{
    /// <summary>Indicates whether the exchange accepted and processed the order successfully.</summary>
    public bool Success { get; set; }

    /// <summary>
    /// The order identifier assigned by the exchange.
    /// Populated on success; null on failure.
    /// </summary>
    public string? ExchangeOrderId { get; set; }

    /// <summary>The resulting order status after the operation (e.g., Filled, PartiallyFilled, Rejected).</summary>
    public OrderStatus Status { get; set; }

    /// <summary>The quantity that was filled as part of this operation.</summary>
    public decimal FilledQuantity { get; set; }

    /// <summary>The weighted average price at which the order was filled. Null if nothing was filled.</summary>
    public decimal? AverageFillPrice { get; set; }

    /// <summary>
    /// The trading fee charged by the exchange for this operation.
    /// Null if no fee information is available or the order failed.
    /// </summary>
    public decimal? Fee { get; set; }

    /// <summary>
    /// The asset in which the fee was denominated (e.g., "BNB", "USDT", "BTC").
    /// Null if no fee information is available.
    /// </summary>
    public string? FeeAsset { get; set; }

    /// <summary>
    /// Human-readable error message when the order operation fails.
    /// Null on success.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>UTC timestamp when this result was created.</summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Creates a successful <see cref="OrderResult"/> with fill details and optional fee information.
    /// </summary>
    /// <param name="exchangeOrderId">The order ID assigned by the exchange.</param>
    /// <param name="status">The current status of the order after execution.</param>
    /// <param name="filledQty">The quantity that was filled.</param>
    /// <param name="avgPrice">The weighted average fill price, or null if not yet filled.</param>
    /// <param name="fee">Optional trading fee charged by the exchange.</param>
    /// <param name="feeAsset">Optional asset denomination of the fee (e.g., "BNB", "USD").</param>
    /// <returns>A new <see cref="OrderResult"/> with <see cref="Success"/> set to <c>true</c>.</returns>
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

    /// <summary>
    /// Creates a failed <see cref="OrderResult"/> with the given error message.
    /// The status is automatically set to <see cref="OrderStatus.Rejected"/>.
    /// </summary>
    /// <param name="error">A human-readable description of why the order failed.</param>
    /// <returns>A new <see cref="OrderResult"/> with <see cref="Success"/> set to <c>false</c>.</returns>
    public static OrderResult Failed(string error) =>
        new()
        {
            Success = false,
            ErrorMessage = error,
            Status = OrderStatus.Rejected
        };
}
