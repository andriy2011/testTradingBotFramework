// =============================================================================
// OrderManagerTests.cs
// Unit tests for the OrderManager service, which is the central orchestrator
// for executing trade signals. It coordinates between:
//   - IPositionManager: validates risk limits before placing orders
//   - IExchangeFactory/IExchangeClient: places orders on the target exchange
//   - IPositionSizer: calculates order quantity when the signal doesn't specify one
//   - IAccountingService: records trade history after successful fills
//
// The OrderManager implements the full signal execution pipeline:
//   1. Check risk limits (skip if exceeded)
//   2. Determine quantity (from signal or position sizer)
//   3. Skip if quantity <= 0
//   4. If DryRunMode, log and return (don't place real order)
//   5. Place order via exchange client
//   6. On success: record position fill + trade record
//   7. On failure: log error, don't record anything
//
// All dependencies are mocked with NSubstitute. Default mock behavior:
//   - Risk limits pass (ValidateRiskLimits returns true)
//   - DryRunMode = false (orders are placed for real by default)
//   - ExchangeFactory returns the mocked exchange client
// Individual tests override specific mocks to test each scenario.
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.OrderManagement;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="OrderManager"/>, the core signal execution engine.
/// Each test focuses on one branch of the ExecuteSignalAsync pipeline.
/// </summary>
public class OrderManagerTests
{
    // Mocked dependencies
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IExchangeClient _exchangeClient;
    private readonly IPositionManager _positionManager;
    private readonly IPositionSizer _positionSizer;
    private readonly IAccountingService _accountingService;
    private readonly ILogger<OrderManager> _logger;
    private readonly TradingSettings _tradingSettings;

    // System under test — created with DryRunMode = false by default
    private readonly OrderManager _sut;

    public OrderManagerTests()
    {
        _exchangeFactory = Substitute.For<IExchangeFactory>();
        _exchangeClient = Substitute.For<IExchangeClient>();
        _positionManager = Substitute.For<IPositionManager>();
        _positionSizer = Substitute.For<IPositionSizer>();
        _accountingService = Substitute.For<IAccountingService>();
        _logger = Substitute.For<ILogger<OrderManager>>();

        // Default settings: live mode (not dry run)
        _tradingSettings = new TradingSettings { DryRunMode = false };

        // Default mock wiring: factory returns the mocked client, risk limits pass
        _exchangeFactory.GetClient(Arg.Any<ExchangeName>()).Returns(_exchangeClient);
        _positionManager.ValidateRiskLimits(Arg.Any<ExchangeName>()).Returns(true);

        _sut = new OrderManager(
            _exchangeFactory,
            _positionManager,
            _positionSizer,
            _accountingService,
            Options.Create(_tradingSettings),
            _logger);
    }

    /// <summary>
    /// Helper method to create a standard Binance BTCUSDT Buy Market signal.
    /// Quantity is configurable — null triggers the position sizer fallback.
    /// </summary>
    private static TradeSignal CreateSignal(decimal? quantity = null) => new()
    {
        Exchange = ExchangeName.Binance,
        Symbol = "BTCUSDT",
        Action = SignalAction.Open,
        Side = OrderSide.Buy,
        OrderType = OrderType.Market,
        Quantity = quantity
    };

    // -------------------------------------------------------------------------
    // Risk limit gate: first check in the pipeline
    // -------------------------------------------------------------------------

    /// <summary>
    /// When risk limits are exceeded (ValidateRiskLimits returns false),
    /// the OrderManager should skip the signal entirely — no order is placed
    /// on any exchange. This prevents overexposure.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_RiskLimitsExceeded_Skips()
    {
        // Override the default: risk limits now fail
        _positionManager.ValidateRiskLimits(ExchangeName.Binance).Returns(false);

        await _sut.ExecuteSignalAsync(CreateSignal(1m));

        // No order should reach the exchange
        await _exchangeClient.DidNotReceive().PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Quantity resolution: signal quantity vs position sizer
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the signal includes an explicit Quantity value, the OrderManager
    /// should use it directly and NOT call the position sizer. The signal
    /// quantity is treated as an override from the signal source.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_UsesSignalQuantity_WhenProvided()
    {
        var signal = CreateSignal(quantity: 5m);
        _exchangeClient.PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(OrderResult.Succeeded("123", OrderStatus.Filled, 5m, 50000m));

        await _sut.ExecuteSignalAsync(signal);

        // Verify the order was placed with the signal's quantity of 5
        await _exchangeClient.Received(1).PlaceOrderAsync(
            Arg.Is<Order>(o => o.Quantity == 5m),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// When the signal has Quantity = null, the OrderManager should fall back
    /// to the IPositionSizer.CalculateQuantityAsync to determine the quantity.
    /// The position sizer uses the fixed-fraction method (balance * percent / price).
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_FallsBackToPositionSizer_WhenQuantityNull()
    {
        var signal = CreateSignal(quantity: null);

        // Position sizer will return 2
        _positionSizer.CalculateQuantityAsync(signal, Arg.Any<CancellationToken>()).Returns(2m);
        _exchangeClient.PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(OrderResult.Succeeded("123", OrderStatus.Filled, 2m, 50000m));

        await _sut.ExecuteSignalAsync(signal);

        // Verify the position sizer was called
        await _positionSizer.Received(1).CalculateQuantityAsync(signal, Arg.Any<CancellationToken>());
        // Verify the order used the sizer's calculated quantity
        await _exchangeClient.Received(1).PlaceOrderAsync(
            Arg.Is<Order>(o => o.Quantity == 2m),
            Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Zero quantity guard: prevents placing meaningless orders
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the calculated quantity is 0 (e.g., the position sizer returns 0
    /// due to insufficient balance or a price of 0), the signal should be skipped.
    /// Placing a zero-quantity order would be rejected by exchanges anyway.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_QuantityZero_Skips()
    {
        var signal = CreateSignal(quantity: null);
        _positionSizer.CalculateQuantityAsync(signal, Arg.Any<CancellationToken>()).Returns(0m);

        await _sut.ExecuteSignalAsync(signal);

        // No order placed
        await _exchangeClient.DidNotReceive().PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Dry run mode: logs but doesn't place real orders
    // -------------------------------------------------------------------------

    /// <summary>
    /// When DryRunMode is enabled in TradingSettings, the OrderManager should
    /// log what it would do but NOT actually place an order on the exchange.
    /// This is used for testing signal pipelines without real money at risk.
    /// A separate SUT is created here with DryRunMode = true.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_DryRunMode_DoesNotPlaceOrder()
    {
        // Create a new OrderManager instance with DryRunMode enabled
        var dryRunSettings = new TradingSettings { DryRunMode = true };
        var dryRunSut = new OrderManager(
            _exchangeFactory, _positionManager, _positionSizer,
            _accountingService, Options.Create(dryRunSettings), _logger);

        await dryRunSut.ExecuteSignalAsync(CreateSignal(1m));

        // Exchange client should never be called in dry run mode
        await _exchangeClient.DidNotReceive().PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    // -------------------------------------------------------------------------
    // Successful fill: records position and trade
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the exchange client returns a successful fill (Success = true,
    /// FilledQuantity > 0, AverageFillPrice has a value), the OrderManager should:
    ///   1. Record the fill in the PositionManager (updates position book)
    ///   2. Record the trade in the AccountingService (updates trade history)
    /// This test verifies both downstream calls happen with the correct parameters.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_SuccessfulFill_RecordsPositionAndTrade()
    {
        var signal = CreateSignal(quantity: 1m);
        _exchangeClient.PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(OrderResult.Succeeded("EX123", OrderStatus.Filled, 1m, 50000m, 0.5m, "USDT"));

        await _sut.ExecuteSignalAsync(signal);

        // Verify position manager received the fill with correct parameters
        _positionManager.Received(1).RecordFill(
            ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);

        // Verify accounting service received a trade record with matching fields
        await _accountingService.Received(1).RecordTradeAsync(
            Arg.Is<TradeRecord>(t =>
                t.Exchange == ExchangeName.Binance &&
                t.Symbol == "BTCUSDT" &&
                t.Quantity == 1m &&
                t.Price == 50000m &&
                t.Fee == 0.5m));
    }

    // -------------------------------------------------------------------------
    // Failed order: no side effects
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the exchange client returns a failure (e.g., "Insufficient margin"),
    /// the OrderManager should NOT record any fill or trade. Failed orders
    /// should leave the position book and trade history unchanged.
    /// </summary>
    [Fact]
    public async Task ExecuteSignalAsync_FailedOrder_DoesNotRecordFill()
    {
        var signal = CreateSignal(quantity: 1m);
        _exchangeClient.PlaceOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>())
            .Returns(OrderResult.Failed("Insufficient margin"));

        await _sut.ExecuteSignalAsync(signal);

        // Neither the position manager nor accounting service should be called
        _positionManager.DidNotReceive().RecordFill(
            Arg.Any<ExchangeName>(), Arg.Any<string>(), Arg.Any<OrderSide>(),
            Arg.Any<decimal>(), Arg.Any<decimal>());
        await _accountingService.DidNotReceive().RecordTradeAsync(Arg.Any<TradeRecord>());
    }
}
