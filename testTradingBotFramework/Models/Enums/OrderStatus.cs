// -----------------------------------------------------------------------
// <file>
//   OrderStatus.cs - Lifecycle states of an order from creation to completion.
//   Tracks the order through local creation, exchange submission, and resolution.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Lifecycle states of an order, tracking it from local creation through
/// exchange submission to final resolution.
/// </summary>
public enum OrderStatus
{
    /// <summary>
    /// Order has been created locally but not yet sent to the exchange.
    /// </summary>
    Pending,

    /// <summary>
    /// Order has been accepted by the exchange and is awaiting execution.
    /// Corresponds to Binance "New" status.
    /// </summary>
    Submitted,

    /// <summary>
    /// Some quantity of the order has been filled, but not the full amount.
    /// </summary>
    PartiallyFilled,

    /// <summary>
    /// Order has been fully executed at the exchange.
    /// </summary>
    Filled,

    /// <summary>
    /// Order was cancelled by the user or system before full execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Order was refused by the exchange (e.g., insufficient margin, invalid parameters).
    /// </summary>
    Rejected,

    /// <summary>
    /// Order expired because its time-in-force window was exceeded
    /// without being filled.
    /// </summary>
    Expired
}
