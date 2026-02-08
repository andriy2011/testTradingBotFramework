// =============================================================================
// ISignalDispatcher.cs
// Routes parsed trade signals to the appropriate handler (OrderManager).
// Decouples signal parsing from order execution for testability and extensibility.
//
// Implemented by: SignalDispatcher
// =============================================================================

using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.EventProcessing;

/// <summary>
/// Routes parsed <see cref="TradeSignal"/> objects to the order execution pipeline.
/// Catches and logs exceptions to prevent signal failures from crashing the Event Hub listener.
/// </summary>
public interface ISignalDispatcher
{
    /// <summary>
    /// Dispatches a trade signal for execution via <see cref="OrderManagement.IOrderManager"/>.
    /// </summary>
    Task DispatchAsync(TradeSignal signal, CancellationToken ct = default);
}
