// =============================================================================
// PositionManagerTests.cs
// Unit tests for the PositionManager service, which tracks all open positions
// in-memory using a ConcurrentDictionary keyed by "Exchange:Symbol".
//
// The PositionManager is the single source of truth for local position state.
// It handles:
//   - Opening new positions (long via Buy, short via Sell)
//   - Averaging entry price when adding to an existing same-side position
//   - Reducing or closing positions when a fill comes in on the opposite side
//   - Enforcing risk limits (max number of open positions per exchange)
//   - Filtering positions by exchange
//   - Updating mark-to-market prices and unrealized P&L
//   - Synchronizing local state with exchange-reported positions
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="PositionManager"/>, which maintains the local position book.
/// Uses real TradingSettings via Options.Create (no mocking needed for simple config).
/// MaxOpenPositions is set to 2 for easy risk-limit testing.
/// </summary>
public class PositionManagerTests
{
    private readonly PositionManager _sut;

    // Logger is stored as a field so we can verify log calls in SyncPositions tests
    private readonly ILogger<PositionManager> _logger;

    public PositionManagerTests()
    {
        _logger = Substitute.For<ILogger<PositionManager>>();

        // MaxOpenPositions = 2 makes it easy to hit the limit in tests
        var settings = Options.Create(new TradingSettings { MaxOpenPositions = 2 });
        _sut = new PositionManager(settings, _logger);
    }

    // -------------------------------------------------------------------------
    // RecordFill: Opening new positions
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a Buy fill is recorded for a symbol with no existing position,
    /// a new Long position should be created with the given quantity and entry price.
    /// Buy side always maps to PositionSide.Long.
    /// </summary>
    [Fact]
    public void RecordFill_Buy_OpensNewLongPosition()
    {
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);

        var positions = _sut.GetOpenPositions();
        positions.Should().HaveCount(1);
        positions[0].Side.Should().Be(PositionSide.Long);
        positions[0].Quantity.Should().Be(1m);
        positions[0].EntryPrice.Should().Be(50000m);
    }

    /// <summary>
    /// When a Sell fill is recorded for a symbol with no existing position,
    /// a new Short position should be created. Sell side maps to PositionSide.Short.
    /// </summary>
    [Fact]
    public void RecordFill_Sell_OpensNewShortPosition()
    {
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Sell, 2m, 50000m);

        var positions = _sut.GetOpenPositions();
        positions.Should().HaveCount(1);
        positions[0].Side.Should().Be(PositionSide.Short);
        positions[0].Quantity.Should().Be(2m);
    }

    // -------------------------------------------------------------------------
    // RecordFill: Adding to existing positions (same side)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a second Buy fill arrives for the same symbol, the position manager
    /// should compute a weighted average entry price:
    ///   avgPrice = (oldPrice * oldQty + newPrice * newQty) / totalQty
    /// Example: (50000*1 + 60000*1) / 2 = 55000
    /// </summary>
    [Fact]
    public void RecordFill_SameSide_AveragesEntryPrice()
    {
        // First fill: 1 BTC at 50,000
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);
        // Second fill: 1 BTC at 60,000 — same side (Buy/Long)
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 60000m);

        var positions = _sut.GetOpenPositions();
        positions.Should().HaveCount(1);
        positions[0].Quantity.Should().Be(2m);
        // Weighted average: (50000*1 + 60000*1) / 2 = 55000
        positions[0].EntryPrice.Should().Be(55000m);
    }

    // -------------------------------------------------------------------------
    // RecordFill: Reducing / closing positions (opposite side)
    // -------------------------------------------------------------------------

    /// <summary>
    /// When a Sell fill arrives against an existing Long position, the quantity
    /// is reduced. The position remains open with the remaining quantity.
    /// Example: Long 3 BTC, then Sell 1 BTC -> Long 2 BTC remaining.
    /// </summary>
    [Fact]
    public void RecordFill_OppositeSide_ReducesQuantity()
    {
        // Open a Long 3 BTC position
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 3m, 50000m);
        // Partial close: sell 1 BTC
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Sell, 1m, 55000m);

        var positions = _sut.GetOpenPositions();
        positions.Should().HaveCount(1);
        positions[0].Quantity.Should().Be(2m);
        positions[0].Side.Should().Be(PositionSide.Long); // still long
    }

    /// <summary>
    /// When a closing fill exactly matches the open quantity (qty -> 0),
    /// the position should be completely removed from the position book.
    /// This prevents stale zero-quantity entries from accumulating.
    /// </summary>
    [Fact]
    public void RecordFill_ClosingFill_RemovesPosition()
    {
        // Open Long 1 BTC
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);
        // Close entirely: sell 1 BTC
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Sell, 1m, 55000m);

        var positions = _sut.GetOpenPositions();
        positions.Should().BeEmpty(); // position fully closed and removed
    }

    // -------------------------------------------------------------------------
    // ValidateRiskLimits: Prevents exceeding max open positions per exchange
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the number of open positions on an exchange equals MaxOpenPositions (2),
    /// ValidateRiskLimits should return false to prevent opening new positions.
    /// This is a critical safety check called before every order placement.
    /// </summary>
    [Fact]
    public void ValidateRiskLimits_MaxPositionsReached_ReturnsFalse()
    {
        // Open 2 positions (the configured max)
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);
        _sut.RecordFill(ExchangeName.Binance, "ETHUSDT", OrderSide.Buy, 10m, 3000m);

        var result = _sut.ValidateRiskLimits(ExchangeName.Binance);

        result.Should().BeFalse(); // at capacity, no more positions allowed
    }

    /// <summary>
    /// When there is still room below MaxOpenPositions, ValidateRiskLimits
    /// should return true, allowing new orders to proceed.
    /// </summary>
    [Fact]
    public void ValidateRiskLimits_BelowMax_ReturnsTrue()
    {
        // Only 1 position open (max is 2)
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);

        var result = _sut.ValidateRiskLimits(ExchangeName.Binance);

        result.Should().BeTrue(); // still room for one more
    }

    // -------------------------------------------------------------------------
    // GetOpenPositions: Filtering by exchange
    // -------------------------------------------------------------------------

    /// <summary>
    /// When positions exist on multiple exchanges, GetOpenPositions with an
    /// exchange filter should return only positions for that specific exchange.
    /// This is used by the dashboard and accounting to show per-exchange views.
    /// </summary>
    [Fact]
    public void GetOpenPositions_FiltersByExchange()
    {
        // One position on Binance, one on Oanda
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);
        _sut.RecordFill(ExchangeName.Oanda, "EUR_USD", OrderSide.Sell, 1000m, 1.1m);

        var binancePositions = _sut.GetOpenPositions(ExchangeName.Binance);
        var oandaPositions = _sut.GetOpenPositions(ExchangeName.Oanda);

        // Each exchange should only see its own positions
        binancePositions.Should().HaveCount(1);
        binancePositions[0].Symbol.Should().Be("BTCUSDT");
        oandaPositions.Should().HaveCount(1);
        oandaPositions[0].Symbol.Should().Be("EUR_USD");
    }

    // -------------------------------------------------------------------------
    // UpdatePositionPrice: Mark-to-market and unrealized P&L calculation
    // -------------------------------------------------------------------------

    /// <summary>
    /// UpdatePositionPrice should update the CurrentPrice and recalculate
    /// UnrealizedPnL using the Position.UpdateCurrentPrice() method.
    /// For a Long position: PnL = (currentPrice - entryPrice) * quantity
    /// Example: (55000 - 50000) * 2 = 10,000 profit
    /// </summary>
    [Fact]
    public void UpdatePositionPrice_UpdatesUnrealizedPnL()
    {
        // Open Long 2 BTC at 50,000
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 2m, 50000m);

        // Price moves up to 55,000
        _sut.UpdatePositionPrice(ExchangeName.Binance, "BTCUSDT", 55000m);

        var positions = _sut.GetOpenPositions();
        positions[0].CurrentPrice.Should().Be(55000m);
        // Long P&L = (55000 - 50000) * 2 = 10,000
        positions[0].UnrealizedPnL.Should().Be(10000m);
    }

    // -------------------------------------------------------------------------
    // SyncPositions: Reconciliation with exchange-reported positions
    // -------------------------------------------------------------------------

    /// <summary>
    /// When SyncPositions detects that the local position quantity differs
    /// from what the exchange reports, it should log a warning about the mismatch.
    /// This is essential for detecting drift between local tracking and actual
    /// exchange state (e.g., due to manual trades or missed fills).
    ///
    /// We verify the warning by checking that the NSubstitute mock logger
    /// received a Log call at Warning level containing "mismatch" in the message.
    /// </summary>
    [Fact]
    public void SyncPositions_QuantityMismatch_LogsWarning()
    {
        // Local state: 1 BTC
        _sut.RecordFill(ExchangeName.Binance, "BTCUSDT", OrderSide.Buy, 1m, 50000m);

        // Exchange reports 2 BTC — a mismatch with local state
        var exchangePositions = new List<Position>
        {
            new()
            {
                Exchange = ExchangeName.Binance,
                Symbol = "BTCUSDT",
                Side = PositionSide.Long,
                Quantity = 2m, // mismatch: local has 1, exchange has 2
                EntryPrice = 50000m
            }
        };

        _sut.SyncPositions(ExchangeName.Binance, exchangePositions);

        // Verify that a Warning-level log message containing "mismatch" was emitted
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("mismatch")),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
