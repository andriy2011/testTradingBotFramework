using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models;
using testTradingBotFramework.Services.OrderManagement;

namespace testTradingBotFramework.Services.EventProcessing;

public class SignalDispatcher : ISignalDispatcher
{
    private readonly IOrderManager _orderManager;
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
