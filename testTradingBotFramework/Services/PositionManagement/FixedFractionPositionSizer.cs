// =============================================================================
// FixedFractionPositionSizer.cs
// Implements the fixed-fraction (fixed-percentage) position sizing method.
//
// Formula:
//   riskAmount = availableBalance * (MaxPositionSizePercent / 100)
//   quantity   = riskAmount / currentPrice
//
// Example with $10,000 balance, 2% max position size, BTC at $50,000:
//   riskAmount = 10000 * (2 / 100) = $200
//   quantity   = 200 / 50000 = 0.004 BTC
//
// This ensures each position uses at most MaxPositionSizePercent of the
// available balance, providing consistent risk management across different
// asset prices and account sizes.
//
// Edge cases:
//   - Price = 0: returns 0 (avoids division by zero)
//   - Exchange API error: catches exception, returns 0 (graceful degradation)
//   - Result is rounded to 8 decimal places (crypto-standard precision)
// =============================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.PositionManagement;

/// <summary>
/// Calculates order quantity using the fixed-fraction money management method.
/// Fetches account balance and current price from the exchange to compute
/// the appropriate position size based on <see cref="TradingSettings.MaxPositionSizePercent"/>.
/// </summary>
public class FixedFractionPositionSizer : IPositionSizer
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly TradingSettings _settings;
    private readonly ILogger<FixedFractionPositionSizer> _logger;

    public FixedFractionPositionSizer(
        IExchangeFactory exchangeFactory,
        IOptions<TradingSettings> settings,
        ILogger<FixedFractionPositionSizer> logger)
    {
        _exchangeFactory = exchangeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Calculates the order quantity for a given signal using fixed-fraction sizing.
    ///
    /// Steps:
    ///   1. Get the exchange client for the signal's target exchange
    ///   2. Fetch the current account balance (available balance)
    ///   3. Fetch the current market price for the symbol
    ///   4. Apply the formula: qty = (balance * percent / 100) / price
    ///   5. Round to 8 decimal places (standard crypto precision)
    ///
    /// Returns 0 on any error (price = 0, API failure, etc.) so the OrderManager
    /// can safely skip the signal without crashing.
    /// </summary>
    /// <param name="signal">The trade signal containing exchange and symbol info.</param>
    /// <param name="ct">Cancellation token for async API calls.</param>
    /// <returns>The calculated quantity, or 0 if sizing cannot be performed.</returns>
    public async Task<decimal> CalculateQuantityAsync(TradeSignal signal, CancellationToken ct = default)
    {
        try
        {
            // Resolve the exchange client and fetch market data
            var client = _exchangeFactory.GetClient(signal.Exchange);
            var balance = await client.GetAccountBalanceAsync(ct);
            var price = await client.GetCurrentPriceAsync(signal.Symbol, ct);

            // Guard against division by zero (e.g., unlisted or halted symbol)
            if (price <= 0)
            {
                _logger.LogWarning("Price is zero or negative for {Symbol}. Cannot size position.", signal.Symbol);
                return 0;
            }

            // Apply fixed-fraction formula:
            // riskAmount = available balance * (max position size % / 100)
            // quantity = riskAmount / current price
            var riskAmount = balance.AvailableBalance * (_settings.MaxPositionSizePercent / 100m);
            var quantity = riskAmount / price;

            _logger.LogDebug("Position sizing for {Symbol}: Balance={Balance}, RiskAmt={Risk}, Price={Price}, Qty={Qty}",
                signal.Symbol, balance.AvailableBalance, riskAmount, price, quantity);

            // Round to 8 decimal places (standard precision for crypto exchanges)
            return Math.Round(quantity, 8);
        }
        catch (Exception ex)
        {
            // Catch any exchange API errors gracefully — return 0 so the
            // OrderManager skips this signal rather than crashing the pipeline
            _logger.LogError(ex, "Failed to calculate position size for {Symbol}", signal.Symbol);
            return 0;
        }
    }
}
