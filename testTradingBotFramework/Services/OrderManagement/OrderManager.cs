// =============================================================================
// OrderManager.cs
// The central orchestrator for executing trade signals. This is the core of
// the trading bot's order execution pipeline, coordinating between multiple
// services to safely place orders on exchanges.
//
// Execution pipeline (ExecuteSignalAsync):
//   1. RISK CHECK: Validate risk limits via PositionManager
//      -> Skip signal if max positions reached
//   2. GET CLIENT: Resolve the exchange client via ExchangeFactory
//   3. DETERMINE QUANTITY: Use signal's quantity if provided, otherwise
//      fall back to the PositionSizer (fixed-fraction money management)
//      -> Skip if quantity is 0 or negative
//   4. DRY RUN CHECK: If DryRunMode is enabled, log the order and return
//      (no real order placed — for testing signal pipelines)
//   5. PLACE ORDER: Submit the order to the exchange via PlaceOrderAsync
//   6. ON SUCCESS: Record the fill in PositionManager + AccountingService
//   7. ON FAILURE: Log the error, do not record anything
//
// Dependencies are injected via constructor for testability. All exchange
// interactions go through the IExchangeFactory/IExchangeClient abstraction.
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Services.OrderManagement;

/// <summary>
/// Executes trade signals by coordinating risk checks, position sizing,
/// order placement, and post-fill recording. Implements <see cref="IOrderManager"/>.
/// </summary>
public class OrderManager : IOrderManager
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IPositionManager _positionManager;
    private readonly IPositionSizer _positionSizer;
    private readonly IAccountingService _accountingService;
    private readonly TradingSettings _tradingSettings;
    private readonly ILogger<OrderManager> _logger;

    public OrderManager(
        IExchangeFactory exchangeFactory,
        IPositionManager positionManager,
        IPositionSizer positionSizer,
        IAccountingService accountingService,
        IOptions<TradingSettings> tradingSettings,
        ILogger<OrderManager> logger)
    {
        _exchangeFactory = exchangeFactory;
        _positionManager = positionManager;
        _positionSizer = positionSizer;
        _accountingService = accountingService;
        _tradingSettings = tradingSettings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executes a trade signal through the full order pipeline.
    /// See class-level documentation for the step-by-step pipeline description.
    /// </summary>
    /// <param name="signal">The trade signal to execute (from SignalParser -> SignalDispatcher).</param>
    /// <param name="ct">Cancellation token for async operations.</param>
    public async Task ExecuteSignalAsync(TradeSignal signal, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing signal {SignalId}: {Exchange} {Symbol} {Action} {Side}",
            signal.SignalId, signal.Exchange, signal.Symbol, signal.Action, signal.Side);

        // STEP 1: Risk limit check — prevent overexposure
        if (!_positionManager.ValidateRiskLimits(signal.Exchange))
        {
            _logger.LogWarning("Risk limits exceeded for {Exchange}. Skipping signal {SignalId}",
                signal.Exchange, signal.SignalId);
            return;
        }

        // STEP 2: Resolve the exchange client for this signal's target exchange
        var client = _exchangeFactory.GetClient(signal.Exchange);

        // STEP 3: Determine order quantity
        // Use signal's explicit quantity if provided; otherwise, calculate via position sizer
        // The null-coalescing operator (??) triggers the async sizer only when signal.Quantity is null
        var quantity = signal.Quantity
            ?? await _positionSizer.CalculateQuantityAsync(signal, ct);

        // Guard: skip if quantity is zero or negative (e.g., sizer returned 0 due to error)
        if (quantity <= 0)
        {
            _logger.LogWarning("Calculated quantity is zero or negative for signal {SignalId}. Skipping.", signal.SignalId);
            return;
        }

        // Build the order object from the signal and calculated quantity
        var order = new Order
        {
            Exchange = signal.Exchange,
            Symbol = signal.Symbol,
            Side = signal.Side,
            OrderType = signal.OrderType,
            Quantity = quantity,
            Price = signal.Price,
            SignalId = signal.SignalId
        };

        // STEP 4: Dry run mode — log the order but don't actually place it
        // Used for testing signal pipelines without risking real money
        if (_tradingSettings.DryRunMode)
        {
            _logger.LogInformation("[DRY RUN] Would place {OrderType} {Side} order for {Quantity} {Symbol} on {Exchange}",
                order.OrderType, order.Side, order.Quantity, order.Symbol, order.Exchange);
            return;
        }

        // STEP 5: Place the order on the exchange
        var result = await client.PlaceOrderAsync(order, ct);

        // STEP 6 or 7: Handle the result
        if (result.Success)
        {
            _logger.LogInformation("Order filled: {ExchangeOrderId} {Status} {FilledQty}@{AvgPrice}",
                result.ExchangeOrderId, result.Status, result.FilledQuantity, result.AverageFillPrice);

            // STEP 6: Record the fill in both the position book and trade history
            // Only record if there was an actual fill (qty > 0 and price available)
            if (result.FilledQuantity > 0 && result.AverageFillPrice.HasValue)
            {
                // Update the local position book
                _positionManager.RecordFill(
                    signal.Exchange,
                    signal.Symbol,
                    signal.Side,
                    result.FilledQuantity,
                    result.AverageFillPrice.Value);

                // Persist the trade record for accounting and history
                await _accountingService.RecordTradeAsync(new TradeRecord
                {
                    OrderId = order.OrderId,
                    ExchangeOrderId = result.ExchangeOrderId,
                    SignalId = signal.SignalId,
                    Exchange = signal.Exchange,
                    Symbol = signal.Symbol,
                    Side = signal.Side,
                    Quantity = result.FilledQuantity,
                    Price = result.AverageFillPrice.Value,
                    Fee = result.Fee ?? 0,
                    FeeAsset = result.FeeAsset ?? string.Empty,
                    Timestamp = result.Timestamp
                });
            }
        }
        else
        {
            // STEP 7: Order failed — log the error, do NOT record anything
            _logger.LogError("Order failed for signal {SignalId}: {Error}", signal.SignalId, result.ErrorMessage);
        }
    }
}
