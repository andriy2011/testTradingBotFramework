// =============================================================================
// AccountingServiceTests.cs
// Unit tests for the AccountingService, which is responsible for:
//   - Recording executed trades (delegating to ITradeHistoryStore)
//   - Maintaining per-exchange account balance snapshots
//   - Computing local P&L snapshots (summing fees and unrealized P&L from
//     the trade history store and position manager)
//   - Generating reconciliation reports that compare local P&L with
//     exchange-reported P&L and flag divergences above a configurable threshold
//
// Dependencies (ITradeHistoryStore, IPositionManager) are mocked with NSubstitute
// to isolate the AccountingService logic. TradingSettings uses a real instance
// via Options.Create with ReconciliationThreshold = 1.0m for easy threshold testing.
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.PositionManagement;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="AccountingService"/>.
/// The accounting service sits between the trade history store and the position manager,
/// aggregating data from both to produce P&L snapshots and reconciliation reports.
/// </summary>
public class AccountingServiceTests
{
    // Mocked dependencies — we control their return values to test AccountingService logic
    private readonly ITradeHistoryStore _tradeStore;
    private readonly IPositionManager _positionManager;
    private readonly AccountingService _sut;

    public AccountingServiceTests()
    {
        _tradeStore = Substitute.For<ITradeHistoryStore>();
        _positionManager = Substitute.For<IPositionManager>();
        var logger = Substitute.For<ILogger<AccountingService>>();

        // ReconciliationThreshold = 1.0 means any P&L difference > 1.0 is flagged as divergence
        var settings = Options.Create(new TradingSettings { ReconciliationThreshold = 1.0m });
        _sut = new AccountingService(_tradeStore, _positionManager, settings, logger);
    }

    // -------------------------------------------------------------------------
    // RecordTradeAsync: Delegation to the trade history store
    // -------------------------------------------------------------------------

    /// <summary>
    /// RecordTradeAsync should simply pass the trade through to the underlying
    /// ITradeHistoryStore.AddAsync method. The AccountingService adds logging
    /// but does not transform the trade object — it's a thin delegation layer.
    /// </summary>
    [Fact]
    public async Task RecordTradeAsync_DelegatesToStore()
    {
        var trade = new TradeRecord
        {
            Exchange = ExchangeName.Binance,
            Symbol = "BTCUSDT",
            Side = OrderSide.Buy,
            Quantity = 1m,
            Price = 50000m,
            Fee = 5m
        };

        await _sut.RecordTradeAsync(trade);

        // Verify the exact trade object was forwarded to the store exactly once
        await _tradeStore.Received(1).AddAsync(trade);
    }

    // -------------------------------------------------------------------------
    // UpdateBalance & GetExchangePnLSnapshot: Per-exchange balance storage
    // -------------------------------------------------------------------------

    /// <summary>
    /// UpdateBalance stores the exchange-reported account balance in a
    /// ConcurrentDictionary keyed by ExchangeName. GetExchangePnLSnapshot
    /// should then return a PnLSnapshot derived from that stored balance.
    /// This verifies the round-trip: store a balance, then retrieve its snapshot.
    /// </summary>
    [Fact]
    public void UpdateBalance_StoresPerExchangeBalance()
    {
        var balance = new AccountBalance
        {
            Exchange = ExchangeName.Binance,
            TotalBalance = 10000m,
            AvailableBalance = 8000m,
            UnrealizedPnL = 500m
        };

        // Act: store the balance
        _sut.UpdateBalance(ExchangeName.Binance, balance);

        // Assert: retrieving the snapshot should reflect the stored unrealized P&L
        var snapshot = _sut.GetExchangePnLSnapshot(ExchangeName.Binance);
        snapshot.Should().NotBeNull();
        snapshot!.UnrealizedPnL.Should().Be(500m);
    }

    // -------------------------------------------------------------------------
    // GetLocalPnLSnapshot: Aggregating fees and unrealized P&L
    // -------------------------------------------------------------------------

    /// <summary>
    /// GetLocalPnLSnapshot computes a P&L summary by:
    ///   1. Summing all trade fees from the trade history store
    ///   2. Summing unrealized P&L from all open positions via the position manager
    ///   3. Counting total trades
    /// This test sets up 2 trades with fees (5 + 3 = 8) and 2 positions with
    /// unrealized P&L (200 + -50 = 150), then verifies the snapshot totals.
    /// </summary>
    [Fact]
    public void GetLocalPnLSnapshot_SumsFeesAndUnrealizedPnL()
    {
        // Arrange: mock the trade store to return 2 trades with known fees
        var trades = new List<TradeRecord>
        {
            new() { Exchange = ExchangeName.Binance, Symbol = "BTCUSDT", Fee = 5m },
            new() { Exchange = ExchangeName.Binance, Symbol = "ETHUSDT", Fee = 3m }
        };
        _tradeStore.GetAll(ExchangeName.Binance).Returns(trades.AsReadOnly());

        // Arrange: mock the position manager to return 2 positions with known P&L
        var positions = new List<Position>
        {
            new() { Exchange = ExchangeName.Binance, Symbol = "BTCUSDT", UnrealizedPnL = 200m },
            new() { Exchange = ExchangeName.Binance, Symbol = "ETHUSDT", UnrealizedPnL = -50m }
        };
        _positionManager.GetOpenPositions(ExchangeName.Binance).Returns(positions.AsReadOnly());

        // Act
        var snapshot = _sut.GetLocalPnLSnapshot(ExchangeName.Binance);

        // Assert: verify aggregated totals
        snapshot.Exchange.Should().Be(ExchangeName.Binance);
        snapshot.TotalFees.Should().Be(8m);          // 5 + 3
        snapshot.UnrealizedPnL.Should().Be(150m);     // 200 + (-50)
        snapshot.TotalTrades.Should().Be(2);
    }

    // -------------------------------------------------------------------------
    // GetExchangePnLSnapshot: Null when no balance has been stored
    // -------------------------------------------------------------------------

    /// <summary>
    /// When no balance has been stored for a given exchange (e.g., before the
    /// first AccountSyncWorker cycle), GetExchangePnLSnapshot should return null.
    /// This is checked by the reconciliation report to decide whether comparison
    /// is possible.
    /// </summary>
    [Fact]
    public void GetExchangePnLSnapshot_NoBalance_ReturnsNull()
    {
        // No balance has been set for Oanda — should return null
        var snapshot = _sut.GetExchangePnLSnapshot(ExchangeName.Oanda);

        snapshot.Should().BeNull();
    }

    // -------------------------------------------------------------------------
    // GetReconciliationReport: Divergence detection
    // -------------------------------------------------------------------------

    /// <summary>
    /// When the absolute difference between local unrealized P&L and exchange-
    /// reported unrealized P&L exceeds the ReconciliationThreshold (1.0),
    /// the report should flag divergence = true. This alerts operators that
    /// local tracking may have drifted from actual exchange state.
    ///
    /// Setup: local P&L = 100, exchange P&L = 105, diff = 5 > threshold of 1
    /// </summary>
    [Fact]
    public void GetReconciliationReport_DetectsDivergenceAboveThreshold()
    {
        // Arrange: set up local P&L data (no trades, one position with P&L = 100)
        _tradeStore.GetAll(ExchangeName.Binance).Returns(new List<TradeRecord>().AsReadOnly());
        var positions = new List<Position>
        {
            new() { Exchange = ExchangeName.Binance, UnrealizedPnL = 100m }
        };
        _positionManager.GetOpenPositions(ExchangeName.Binance).Returns(positions.AsReadOnly());

        // Arrange: exchange reports P&L = 105 (diff = 5, above threshold of 1)
        _sut.UpdateBalance(ExchangeName.Binance, new AccountBalance
        {
            Exchange = ExchangeName.Binance,
            UnrealizedPnL = 105m
        });

        // Act
        var (local, exchange, diverged) = _sut.GetReconciliationReport(ExchangeName.Binance);

        // Assert: divergence detected because |100 - 105| = 5 > 1.0 threshold
        diverged.Should().BeTrue();
        local.UnrealizedPnL.Should().Be(100m);
        exchange.Should().NotBeNull();
        exchange!.UnrealizedPnL.Should().Be(105m);
    }

    /// <summary>
    /// When the P&L difference is within the ReconciliationThreshold,
    /// the report should NOT flag divergence. Small differences are expected
    /// due to timing between local calculations and exchange snapshots.
    ///
    /// Setup: local P&L = 100, exchange P&L = 100.5, diff = 0.5 <= threshold of 1
    /// </summary>
    [Fact]
    public void GetReconciliationReport_WithinThreshold_ReportsOk()
    {
        // Arrange: local P&L = 100
        _tradeStore.GetAll(ExchangeName.Binance).Returns(new List<TradeRecord>().AsReadOnly());
        var positions = new List<Position>
        {
            new() { Exchange = ExchangeName.Binance, UnrealizedPnL = 100m }
        };
        _positionManager.GetOpenPositions(ExchangeName.Binance).Returns(positions.AsReadOnly());

        // Arrange: exchange P&L = 100.5 (diff = 0.5, within threshold of 1)
        _sut.UpdateBalance(ExchangeName.Binance, new AccountBalance
        {
            Exchange = ExchangeName.Binance,
            UnrealizedPnL = 100.5m
        });

        // Act
        var (_, _, diverged) = _sut.GetReconciliationReport(ExchangeName.Binance);

        // Assert: no divergence — difference is acceptably small
        diverged.Should().BeFalse();
    }
}
