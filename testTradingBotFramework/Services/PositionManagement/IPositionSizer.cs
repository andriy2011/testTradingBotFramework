// =============================================================================
// IPositionSizer.cs
// Money management abstraction — calculates order quantity based on account
// balance, risk parameters, and current market price. Returns 0 on error
// so OrderManager can safely skip the signal.
//
// Implemented by: FixedFractionPositionSizer
//   Formula: qty = (balance * MaxPositionSizePercent / 100) / price
// =============================================================================

using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.PositionManagement;

/// <summary>
/// Calculates order quantity for a trade signal using money management rules.
/// Returns 0 on any error (price unavailable, API failure) for graceful degradation.
/// </summary>
public interface IPositionSizer
{
    /// <summary>
    /// Calculates the appropriate order quantity based on account balance and risk settings.
    /// </summary>
    Task<decimal> CalculateQuantityAsync(TradeSignal signal, CancellationToken ct = default);
}
