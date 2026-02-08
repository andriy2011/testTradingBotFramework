// -----------------------------------------------------------------------
// SignalDispatcher.cs
//
// Routes parsed trade signals from the event processing layer to the
// OrderManager for execution. Acts as a decoupling buffer between event
// ingestion (EventHubListenerService) and order execution (IOrderManager).
//
// Error handling: exceptions thrown by OrderManager.ExecuteSignalAsync()
// are caught and logged here so that a single failed signal does not crash
// the Event Hub listener or prevent subsequent signals from processing.
//
// Future extensions could add:
//   - Routing logic per exchange or signal type (e.g., different handlers).
//   - Signal filtering/validation before execution.
//   - Dead-letter queue for repeatedly failing signals.
//   - Metrics/telemetry for dispatch latency and failure rates.
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models;
using testTradingBotFramework.Services.OrderManagement;

namespace testTradingBotFramework.Services.EventProcessing;

/// <summary>
/// Implements <see cref="ISignalDispatcher"/> by forwarding each
/// <see cref="TradeSignal"/> to the <see cref="IOrderManager"/> for
/// execution. Provides structured logging and fault isolation so that
/// failures in order execution do not propagate to the event consumer.
/// </summary>
public class SignalDispatcher : ISignalDispatcher
{
    /// <summary>The order manager responsible for translating signals into exchange orders.</summary>
    private readonly IOrderManager _orderManager;

    /// <summary>Logger for dispatch activity and execution failure diagnostics.</summary>
    private readonly ILogger<SignalDispatcher> _logger;

    public SignalDispatcher(IOrderManager orderManager, ILogger<SignalDispatcher> logger)
    {
        _orderManager = orderManager;
        _logger = logger;
    }

    public async Task DispatchAsync(TradeSignal signal, CancellationToken ct = default)
    {
        _logger.LogInformation("Dispatching signal {SignalId} → {Exchange}:{Symbol} {Action} {Side}",
            signal.SignalId, signal.Exchange, signal.Symbol, signal.Action, signal.Side);

        try
        {
            await _orderManager.ExecuteSignalAsync(signal, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute signal {SignalId}", signal.SignalId);
        }
    }
}
