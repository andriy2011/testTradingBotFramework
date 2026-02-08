// =============================================================================
// IOrderManager.cs
// Central order execution interface. Orchestrates the full pipeline:
//   Risk check → Position sizing → Order placement → Fill recording
//
// Implemented by: OrderManager
// =============================================================================

using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.OrderManagement;

/// <summary>
/// Executes trade signals through the complete order pipeline including risk validation,
/// position sizing, exchange order placement, and post-fill recording.
/// </summary>
public interface IOrderManager
{
    /// <summary>
    /// Executes a trade signal through the full pipeline. Skips silently if risk limits
    /// are exceeded, quantity is zero, or dry-run mode is enabled.
    /// </summary>
    Task ExecuteSignalAsync(TradeSignal signal, CancellationToken ct = default);
}
