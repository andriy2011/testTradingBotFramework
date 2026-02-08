// =============================================================================
// ITradeHistoryStore.cs
// Persistence abstraction for trade records. Allows swapping storage backends
// (in-memory for dev/testing, database for production) without changing
// business logic.
//
// Implemented by: InMemoryTradeHistoryStore (ConcurrentBag-backed, dev/testing)
// =============================================================================

using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.Accounting;

/// <summary>
/// Persistence interface for <see cref="TradeRecord"/> storage and retrieval.
/// Supports filtering by exchange and symbol with case-insensitive matching.
/// </summary>
public interface ITradeHistoryStore
{
    /// <summary>Adds a trade record to the store.</summary>
    Task AddAsync(TradeRecord trade);

    /// <summary>Returns all trades, optionally filtered by exchange.</summary>
    IReadOnlyList<TradeRecord> GetAll(ExchangeName? exchange = null);

    /// <summary>Queries trades with optional exchange, symbol (case-insensitive), and limit filters.
    /// Results are ordered by descending timestamp.</summary>
    Task<IReadOnlyList<TradeRecord>> QueryAsync(ExchangeName? exchange = null, string? symbol = null, int? limit = null);
}
