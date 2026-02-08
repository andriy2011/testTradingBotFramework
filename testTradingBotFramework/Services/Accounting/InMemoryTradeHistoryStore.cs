// =============================================================================
// InMemoryTradeHistoryStore.cs
// A simple, thread-safe in-memory implementation of ITradeHistoryStore.
// Uses ConcurrentBag for thread-safe insertions from multiple workers.
//
// Supports:
//   - Adding trades (thread-safe via ConcurrentBag)
//   - Retrieving all trades with optional exchange filter (GetAll)
//   - Querying with exchange + symbol filters and limit (QueryAsync)
//   - All results are ordered by timestamp descending (newest first)
//
// Note: This is intended for development/testing. For production, this would
// be replaced with a database-backed implementation. Data is lost on restart.
// =============================================================================

using System.Collections.Concurrent;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.Accounting;

/// <summary>
/// In-memory trade history store backed by <see cref="ConcurrentBag{T}"/>.
/// Thread-safe for concurrent writes from the OrderManager.
/// Registered as a singleton in DI so all services share the same instance.
/// </summary>
public class InMemoryTradeHistoryStore : ITradeHistoryStore
{
    /// <summary>
    /// Thread-safe collection of all trade records. ConcurrentBag allows
    /// lock-free additions from multiple threads (e.g., concurrent signal processing).
    /// </summary>
    private readonly ConcurrentBag<TradeRecord> _trades = [];

    /// <summary>
    /// Adds a trade record to the in-memory store.
    /// Returns a completed task since the operation is synchronous (in-memory).
    /// </summary>
    /// <param name="trade">The trade record to add.</param>
    public Task AddAsync(TradeRecord trade)
    {
        _trades.Add(trade);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves all trade records, optionally filtered by exchange.
    /// Results are always ordered by timestamp descending (newest first).
    /// </summary>
    /// <param name="exchange">If specified, only returns trades for this exchange.</param>
    /// <returns>A read-only list of trade records sorted by descending timestamp.</returns>
    public IReadOnlyList<TradeRecord> GetAll(ExchangeName? exchange = null)
    {
        var query = _trades.AsEnumerable();
        if (exchange.HasValue)
            query = query.Where(t => t.Exchange == exchange.Value);
        return query.OrderByDescending(t => t.Timestamp).ToList().AsReadOnly();
    }

    /// <summary>
    /// Queries trade records with optional filters for exchange, symbol, and limit.
    /// Symbol matching is case-insensitive (OrdinalIgnoreCase) to handle different
    /// casing from signal sources (e.g., "btcusdt" vs "BTCUSDT").
    /// Results are ordered by timestamp descending before applying the limit.
    /// </summary>
    /// <param name="exchange">Optional exchange filter.</param>
    /// <param name="symbol">Optional symbol filter (case-insensitive).</param>
    /// <param name="limit">Optional maximum number of results to return.</param>
    /// <returns>A read-only list of matching trade records.</returns>
    public Task<IReadOnlyList<TradeRecord>> QueryAsync(
        ExchangeName? exchange = null, string? symbol = null, int? limit = null)
    {
        var query = _trades.AsEnumerable();

        // Apply optional exchange filter
        if (exchange.HasValue)
            query = query.Where(t => t.Exchange == exchange.Value);

        // Apply optional symbol filter (case-insensitive for flexibility)
        if (!string.IsNullOrWhiteSpace(symbol))
            query = query.Where(t => t.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));

        // Always sort by newest first
        query = query.OrderByDescending(t => t.Timestamp);

        // Apply optional limit (for pagination / recent trades display)
        if (limit.HasValue)
            query = query.Take(limit.Value);

        IReadOnlyList<TradeRecord> result = query.ToList().AsReadOnly();
        return Task.FromResult(result);
    }
}
