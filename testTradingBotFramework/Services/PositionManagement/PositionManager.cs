// =============================================================================
// PositionManager.cs
// Maintains the local position book — the single source of truth for what
// positions the bot believes it holds on each exchange.
//
// Positions are stored in a ConcurrentDictionary keyed by "Exchange:Symbol"
// (e.g., "Binance:BTCUSDT") for thread-safe access from multiple workers.
//
// Key responsibilities:
//   - RecordFill: Opens, increases, reduces, or closes positions based on fills
//   - ValidateRiskLimits: Enforces MaxOpenPositions per exchange
//   - GetOpenPositions: Returns current positions, optionally filtered by exchange
//   - UpdatePositionPrice: Mark-to-market price updates (from price monitors)
//   - SyncPositions: Compares local state with exchange-reported positions
//     and logs warnings on mismatches (reconciliation)
// =============================================================================

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PositionManagement;

/// <summary>
/// Thread-safe in-memory position tracker. All position mutations go through
/// <see cref="RecordFill"/> which uses ConcurrentDictionary.AddOrUpdate for atomicity.
/// </summary>
public class PositionManager : IPositionManager
{
    /// <summary>
    /// The position book: maps "Exchange:Symbol" keys to Position objects.
    /// ConcurrentDictionary ensures thread-safe reads/writes from multiple workers.
    /// </summary>
    private readonly ConcurrentDictionary<string, Position> _positions = new();

    private readonly TradingSettings _settings;
    private readonly ILogger<PositionManager> _logger;

    public PositionManager(IOptions<TradingSettings> settings, ILogger<PositionManager> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generates the dictionary key for a position: "Exchange:Symbol".
    /// Example: "Binance:BTCUSDT" or "Oanda:EUR_USD".
    /// </summary>
    private static string PositionKey(ExchangeName exchange, string symbol) => $"{exchange}:{symbol}";

    /// <summary>
    /// Records a fill from an executed order. This is the primary mutation method
    /// for the position book. Behavior depends on whether a position already exists:
    ///
    /// No existing position:
    ///   Creates a new position (Long for Buy, Short for Sell)
    ///
    /// Existing position, same side (e.g., Buy into existing Long):
    ///   Increases quantity and computes weighted average entry price:
    ///   avgPrice = (oldPrice * oldQty + newPrice * newQty) / totalQty
    ///
    /// Existing position, opposite side (e.g., Sell into existing Long):
    ///   Reduces quantity. If quantity reaches 0, the position is removed
    ///   from the dictionary entirely (prevents stale zero-qty entries).
    /// </summary>
    /// <param name="exchange">The exchange where the fill occurred.</param>
    /// <param name="symbol">The trading instrument symbol.</param>
    /// <param name="side">Buy or Sell — determines if this is same-side or opposite-side.</param>
    /// <param name="quantity">The filled quantity (always positive).</param>
    /// <param name="price">The fill price.</param>
    public void RecordFill(ExchangeName exchange, string symbol, OrderSide side, decimal quantity, decimal price)
    {
        var key = PositionKey(exchange, symbol);

        // Map order side to position side: Buy -> Long, Sell -> Short
        var positionSide = side == OrderSide.Buy ? PositionSide.Long : PositionSide.Short;

        // AddOrUpdate is atomic: the "add" factory runs if the key doesn't exist,
        // the "update" factory runs if it does. This prevents race conditions.
        _positions.AddOrUpdate(key,
            // ADD FACTORY: No existing position — create a new one
            _ =>
            {
                _logger.LogInformation("New position opened: {Exchange}:{Symbol} {Side} {Qty}@{Price}",
                    exchange, symbol, positionSide, quantity, price);
                return new Position
                {
                    Exchange = exchange,
                    Symbol = symbol,
                    Side = positionSide,
                    Quantity = quantity,
                    EntryPrice = price,
                    CurrentPrice = price
                };
            },
            // UPDATE FACTORY: Position already exists
            (_, existing) =>
            {
                if (existing.Side == positionSide)
                {
                    // SAME SIDE: Increase position and compute weighted average entry price
                    // Formula: avgPrice = (oldPrice * oldQty + newPrice * newQty) / totalQty
                    var totalCost = existing.EntryPrice * existing.Quantity + price * quantity;
                    existing.Quantity += quantity;
                    existing.EntryPrice = totalCost / existing.Quantity;
                    _logger.LogInformation("Position increased: {Exchange}:{Symbol} now {Qty}@{AvgPrice}",
                        exchange, symbol, existing.Quantity, existing.EntryPrice);
                }
                else
                {
                    // OPPOSITE SIDE: Reduce position (partial or full close)
                    existing.Quantity -= quantity;
                    if (existing.Quantity <= 0)
                    {
                        // Position fully closed (or over-closed)
                        _logger.LogInformation("Position closed: {Exchange}:{Symbol}", exchange, symbol);
                        existing.Quantity = 0;
                    }
                    else
                    {
                        _logger.LogInformation("Position reduced: {Exchange}:{Symbol} now {Qty}",
                            exchange, symbol, existing.Quantity);
                    }
                }
                existing.LastUpdatedAt = DateTimeOffset.UtcNow;
                return existing;
            });

        // Clean up: remove fully closed positions from the dictionary
        // to keep the position book tidy and prevent them from counting
        // toward MaxOpenPositions in risk limit checks
        if (_positions.TryGetValue(key, out var pos) && pos.Quantity <= 0)
        {
            _positions.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Compares local position state with positions reported by the exchange.
    /// Logs warnings for three types of discrepancies:
    ///   1. Position exists locally but NOT on exchange (possible missed close)
    ///   2. Position exists on exchange but NOT locally (possible missed fill)
    ///   3. Both exist but quantities differ (partial fill tracking issue)
    ///
    /// This method does NOT auto-correct — it only logs for manual investigation.
    /// Called periodically by the PositionSyncWorker.
    /// </summary>
    /// <param name="exchange">The exchange to sync positions for.</param>
    /// <param name="exchangePositions">Positions reported by the exchange API.</param>
    public void SyncPositions(ExchangeName exchange, IReadOnlyList<Position> exchangePositions)
    {
        // Get all local positions for this exchange
        var localPositions = _positions
            .Where(kvp => kvp.Value.Exchange == exchange)
            .ToList();

        // Build a set of exchange position keys for fast lookup
        var exchangeKeys = exchangePositions
            .Select(p => PositionKey(p.Exchange, p.Symbol))
            .ToHashSet();

        // Check 1: positions that exist locally but not on exchange
        foreach (var kvp in localPositions)
        {
            if (!exchangeKeys.Contains(kvp.Key))
            {
                _logger.LogWarning("Position {Key} exists locally but not on exchange. Possible reconciliation issue.",
                    kvp.Key);
            }
        }

        // Check 2 & 3: positions on exchange — compare with local state
        foreach (var exchangePos in exchangePositions)
        {
            var key = PositionKey(exchangePos.Exchange, exchangePos.Symbol);
            if (!_positions.ContainsKey(key))
            {
                // Position exists on exchange but not locally
                _logger.LogWarning("Position {Key} exists on exchange but not locally. Exchange: Qty={Qty} Side={Side}",
                    key, exchangePos.Quantity, exchangePos.Side);
            }
            else
            {
                // Both exist — check if quantities match (tolerance: 0.0001 for rounding)
                var local = _positions[key];
                if (Math.Abs(local.Quantity - exchangePos.Quantity) > 0.0001m)
                {
                    _logger.LogWarning("Position quantity mismatch for {Key}: Local={LocalQty}, Exchange={ExchangeQty}",
                        key, local.Quantity, exchangePos.Quantity);
                }
            }
        }
    }

    /// <summary>
    /// Checks whether the number of open positions on the given exchange
    /// is below the configured MaxOpenPositions limit.
    /// Called by OrderManager before placing any order.
    /// </summary>
    /// <param name="exchange">The exchange to check.</param>
    /// <returns>True if a new position can be opened; false if at capacity.</returns>
    public bool ValidateRiskLimits(ExchangeName exchange)
    {
        var openCount = _positions.Count(kvp => kvp.Value.Exchange == exchange);
        if (openCount >= _settings.MaxOpenPositions)
        {
            _logger.LogWarning("Max open positions ({Max}) reached for {Exchange}", _settings.MaxOpenPositions, exchange);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Returns a snapshot of all currently open positions, optionally filtered
    /// by exchange. Returns a read-only list to prevent external mutation.
    /// </summary>
    /// <param name="exchange">If specified, only returns positions for this exchange.</param>
    /// <returns>A read-only list of open positions.</returns>
    public IReadOnlyList<Position> GetOpenPositions(ExchangeName? exchange = null)
    {
        var positions = _positions.Values.AsEnumerable();
        if (exchange.HasValue)
            positions = positions.Where(p => p.Exchange == exchange.Value);
        return positions.ToList().AsReadOnly();
    }

    /// <summary>
    /// Updates the current market price for a position and recalculates its
    /// unrealized P&L. Called by PriceMonitorWorker when new prices arrive.
    /// Delegates to <see cref="Position.UpdateCurrentPrice"/> which computes:
    ///   Long P&L  = (currentPrice - entryPrice) * quantity
    ///   Short P&L = (entryPrice - currentPrice) * quantity
    /// </summary>
    /// <param name="exchange">The exchange the position is on.</param>
    /// <param name="symbol">The instrument symbol.</param>
    /// <param name="currentPrice">The latest market price.</param>
    public void UpdatePositionPrice(ExchangeName exchange, string symbol, decimal currentPrice)
    {
        var key = PositionKey(exchange, symbol);
        if (_positions.TryGetValue(key, out var position))
        {
            position.UpdateCurrentPrice(currentPrice);
        }
    }
}
