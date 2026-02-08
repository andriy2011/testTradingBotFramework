// =============================================================================
// IPositionManager.cs
// Manages the local position book — an in-memory record of all open positions.
// Provides fill recording, position sync with exchanges, risk validation, and
// real-time P&L updates via price feed integration.
//
// Implemented by: PositionManager (ConcurrentDictionary-backed)
// =============================================================================

using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.PositionManagement;

/// <summary>
/// Manages the local position book. Tracks open positions, records fills,
/// validates risk limits, and reconciles with exchange-reported positions.
/// </summary>
public interface IPositionManager
{
    /// <summary>
    /// Records an order fill. Opens new positions, averages into existing same-side
    /// positions, or reduces/closes opposite-side positions.
    /// </summary>
    void RecordFill(ExchangeName exchange, string symbol, OrderSide side, decimal quantity, decimal price);

    /// <summary>
    /// Reconciles local positions with exchange-reported positions.
    /// Logs warnings on quantity mismatches, missing local positions, or extra local positions.
    /// </summary>
    void SyncPositions(ExchangeName exchange, IReadOnlyList<Position> exchangePositions);

    /// <summary>
    /// Returns <c>false</c> if the number of open positions has reached <c>MaxOpenPositions</c>.
    /// </summary>
    bool ValidateRiskLimits(ExchangeName exchange);

    /// <summary>
    /// Returns all open positions, optionally filtered by exchange.
    /// </summary>
    IReadOnlyList<Position> GetOpenPositions(ExchangeName? exchange = null);

    /// <summary>
    /// Updates the current price and recalculates unrealized P&amp;L for a position.
    /// Called by PriceMonitorWorker on each price tick.
    /// </summary>
    void UpdatePositionPrice(ExchangeName exchange, string symbol, decimal currentPrice);
}
