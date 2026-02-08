// =============================================================================
// FixedFractionPositionSizerTests.cs
// Unit tests for FixedFractionPositionSizer, which calculates order quantity
// using the fixed-fraction money management method:
//
//   quantity = (availableBalance * maxPositionSizePercent / 100) / currentPrice
//
// For example, with a $10,000 balance, 2% risk, and BTC at $50,000:
//   riskAmount = 10000 * (2 / 100) = 200
//   quantity   = 200 / 50000 = 0.004 BTC
//
// The sizer fetches the account balance and current price from the exchange
// client via IExchangeFactory. Both are mocked to isolate the calculation logic.
//
// Edge cases tested:
//   - Price = 0 (would cause division by zero) returns 0
//   - Exchange client throws an exception (network error, etc.) returns 0
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="FixedFractionPositionSizer"/>.
/// MaxPositionSizePercent is set to 2.0% for all tests.
/// The exchange factory and client are mocked so no real API calls are made.
/// </summary>
public class FixedFractionPositionSizerTests
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IExchangeClient _exchangeClient;
    private readonly FixedFractionPositionSizer _sut;

    public FixedFractionPositionSizerTests()
    {
        _exchangeFactory = Substitute.For<IExchangeFactory>();
        _exchangeClient = Substitute.For<IExchangeClient>();

        // Wire up: factory returns the mocked client for any exchange
        _exchangeFactory.GetClient(Arg.Any<ExchangeName>()).Returns(_exchangeClient);

        var logger = Substitute.For<ILogger<FixedFractionPositionSizer>>();
        // 2% position size for easy mental math in tests
        var settings = Options.Create(new TradingSettings { MaxPositionSizePercent = 2.0m });
        _sut = new FixedFractionPositionSizer(_exchangeFactory, settings, logger);
    }

    /// <summary>
    /// Helper to create a minimal Binance BTCUSDT Buy signal for the sizer.
    /// The sizer only uses signal.Exchange and signal.Symbol from the signal.
    /// </summary>
    private static TradeSignal CreateSignal() => new()
    {
        Exchange = ExchangeName.Binance,
        Symbol = "BTCUSDT",
        Side = OrderSide.Buy
    };

    // -------------------------------------------------------------------------
    // Happy path: correct quantity calculation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies the core fixed-fraction formula:
    ///   riskAmount = balance * (percent / 100) = 10000 * 0.02 = 200
    ///   quantity   = riskAmount / price = 200 / 50000 = 0.004
    /// The result is rounded to 8 decimal places (crypto standard precision).
    /// </summary>
    [Fact]
    public async Task CalculateQuantityAsync_ReturnsCorrectQuantity()
    {
        // Mock the exchange client to return known values
        _exchangeClient.GetAccountBalanceAsync(Arg.Any<CancellationToken>())
            .Returns(new AccountBalance { AvailableBalance = 10000m });
        _exchangeClient.GetCurrentPriceAsync("BTCUSDT", Arg.Any<CancellationToken>())
            .Returns(50000m);

        var result = await _sut.CalculateQuantityAsync(CreateSignal());

        // 10000 * (2/100) / 50000 = 0.004
        result.Should().Be(0.004m);
    }

    // -------------------------------------------------------------------------
    // Edge case: price is zero (avoid division by zero)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the current price is zero (e.g., exchange returns 0 for an
    /// unlisted or halted asset), the sizer should return 0 to prevent
    /// a division-by-zero error. The sizer explicitly checks for price <= 0.
    /// </summary>
    [Fact]
    public async Task CalculateQuantityAsync_PriceIsZero_ReturnsZero()
    {
        _exchangeClient.GetAccountBalanceAsync(Arg.Any<CancellationToken>())
            .Returns(new AccountBalance { AvailableBalance = 10000m });
        _exchangeClient.GetCurrentPriceAsync("BTCUSDT", Arg.Any<CancellationToken>())
            .Returns(0m); // zero price — would cause division by zero

        var result = await _sut.CalculateQuantityAsync(CreateSignal());

        result.Should().Be(0m); // safely returns 0 instead of throwing
    }

    // -------------------------------------------------------------------------
    // Error handling: exchange client exception
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the exchange client throws an exception (network timeout, API error,
    /// authentication failure, etc.), the sizer should catch it, log the error,
    /// and return 0. This prevents a single exchange outage from crashing the
    /// entire signal processing pipeline.
    /// </summary>
    [Fact]
    public async Task CalculateQuantityAsync_ExceptionFromClient_ReturnsZero()
    {
        // Simulate a network error when fetching the balance
        _exchangeClient.GetAccountBalanceAsync(Arg.Any<CancellationToken>())
            .Throws(new HttpRequestException("Connection refused"));

        var result = await _sut.CalculateQuantityAsync(CreateSignal());

        result.Should().Be(0m); // gracefully returns 0 on exception
    }
}
