// =============================================================================
// InMemoryTradeHistoryStoreTests.cs
// Unit tests for InMemoryTradeHistoryStore, a ConcurrentBag-backed implementation
// of ITradeHistoryStore that holds all trade records in memory.
//
// This store is used by the AccountingService to persist trade records and
// query them for P&L calculations and history display. It supports:
//   - Adding trades (thread-safe via ConcurrentBag)
//   - Retrieving all trades, optionally filtered by exchange
//   - Querying with filters (exchange, symbol) and pagination (limit)
//   - Results are always sorted by timestamp descending (newest first)
//
// No mocking is needed here because this is a concrete, self-contained data store
// with no external dependencies.
// =============================================================================

using FluentAssertions;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="InMemoryTradeHistoryStore"/>.
/// Each test gets a fresh store instance (created in the field initializer),
/// so tests are fully isolated from each other.
/// </summary>
public class InMemoryTradeHistoryStoreTests
{
    // Fresh store for each test — no shared state between tests
    private readonly InMemoryTradeHistoryStore _sut = new();

    // -------------------------------------------------------------------------
    // Basic CRUD: Add and retrieve
    // -------------------------------------------------------------------------

    /// <summary>
    /// Verifies the basic round-trip: add a trade via AddAsync, then retrieve
    /// it via GetAll. The returned trade should have matching field values.
    /// </summary>
    [Fact]
    public async Task AddAndRetrieve_ReturnsTrades()
    {
        var trade = new TradeRecord
        {
            Exchange = ExchangeName.Binance,
            Symbol = "BTCUSDT",
            Side = OrderSide.Buy,
            Quantity = 1m,
            Price = 50000m
        };

        await _sut.AddAsync(trade);
        var result = _sut.GetAll();

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("BTCUSDT");
    }

    // -------------------------------------------------------------------------
    // GetAll: Exchange filtering
    // -------------------------------------------------------------------------

    /// <summary>
    /// GetAll with an exchange parameter should return only trades belonging
    /// to that exchange. Trades on other exchanges should be excluded.
    /// </summary>
    [Fact]
    public async Task GetAll_FiltersByExchange()
    {
        // Add one Binance trade and one Oanda trade
        await _sut.AddAsync(new TradeRecord { Exchange = ExchangeName.Binance, Symbol = "BTCUSDT" });
        await _sut.AddAsync(new TradeRecord { Exchange = ExchangeName.Oanda, Symbol = "EUR_USD" });

        // Filter for Binance only
        var result = _sut.GetAll(ExchangeName.Binance);

        result.Should().HaveCount(1);
        result[0].Exchange.Should().Be(ExchangeName.Binance);
    }

    // -------------------------------------------------------------------------
    // QueryAsync: Symbol filtering (case-insensitive)
    // -------------------------------------------------------------------------

    /// <summary>
    /// QueryAsync supports filtering by symbol using case-insensitive comparison
    /// (StringComparison.OrdinalIgnoreCase). This is important because signal
    /// sources may send symbols in different casings (e.g., "btcusdt" vs "BTCUSDT").
    /// </summary>
    [Fact]
    public async Task QueryAsync_FiltersBySymbol_CaseInsensitive()
    {
        await _sut.AddAsync(new TradeRecord { Exchange = ExchangeName.Binance, Symbol = "BTCUSDT" });
        await _sut.AddAsync(new TradeRecord { Exchange = ExchangeName.Binance, Symbol = "ETHUSDT" });

        // Query with lowercase "btcusdt" — should still match "BTCUSDT"
        var result = await _sut.QueryAsync(symbol: "btcusdt");

        result.Should().HaveCount(1);
        result[0].Symbol.Should().Be("BTCUSDT");
    }

    // -------------------------------------------------------------------------
    // QueryAsync: Limit (pagination)
    // -------------------------------------------------------------------------

    /// <summary>
    /// QueryAsync with a limit parameter should return at most that many trades.
    /// This is used by the dashboard and API to show recent trades without
    /// loading the entire history.
    /// </summary>
    [Fact]
    public async Task QueryAsync_RespectsLimit()
    {
        // Insert 5 trades with staggered timestamps
        for (int i = 0; i < 5; i++)
        {
            await _sut.AddAsync(new TradeRecord
            {
                Exchange = ExchangeName.Binance,
                Symbol = "BTCUSDT",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i)
            });
        }

        // Request only 2
        var result = await _sut.QueryAsync(limit: 2);

        result.Should().HaveCount(2);
    }

    // -------------------------------------------------------------------------
    // Ordering: Descending by timestamp
    // -------------------------------------------------------------------------

    /// <summary>
    /// Both GetAll and QueryAsync should return results ordered by timestamp
    /// descending (newest first). This ensures the most recent trades appear
    /// at the top of trade history displays.
    /// </summary>
    [Fact]
    public async Task Results_OrderedByDescendingTimestamp()
    {
        // Insert oldest first, then newest
        var oldest = new TradeRecord
        {
            Exchange = ExchangeName.Binance,
            Symbol = "BTCUSDT",
            Timestamp = DateTimeOffset.UtcNow.AddHours(-2)
        };
        var newest = new TradeRecord
        {
            Exchange = ExchangeName.Binance,
            Symbol = "BTCUSDT",
            Timestamp = DateTimeOffset.UtcNow
        };

        await _sut.AddAsync(oldest);
        await _sut.AddAsync(newest);

        var result = _sut.GetAll();

        // First result should be the newest (descending order)
        result[0].Timestamp.Should().BeAfter(result[1].Timestamp);
    }
}
