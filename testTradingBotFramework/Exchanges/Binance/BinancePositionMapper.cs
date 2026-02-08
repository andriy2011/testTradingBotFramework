// ============================================================================
// BinancePositionMapper.cs
//
// Static mapper that converts Binance USD-M Futures position data
// (BinancePositionDetailsUsdt) into the framework's local Position model.
//
// Binance represents position direction via signed quantity:
//   - Positive quantity  ->  Long position
//   - Negative quantity  ->  Short position
//
// This mapper normalizes the quantity to its absolute value and derives
// the PositionSide enum from the sign. It also maps Binance's mark price
// (used for unrealized PnL calculations and liquidation) as the current
// price in the local model.
// ============================================================================

using Binance.Net.Objects.Models.Futures;
using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges.Binance;

/// <summary>
/// Provides static mapping from Binance USD-M Futures position details
/// to the framework's local <see cref="Position"/> domain model.
/// </summary>
public static class BinancePositionMapper
{
    /// <summary>
    /// Converts a Binance USDT-margined futures position into the local
    /// <see cref="Position"/> model. Determines position side from the sign
    /// of the quantity and stores the absolute quantity.
    /// </summary>
    /// <param name="pos">
    /// The Binance position details, including signed quantity, entry price,
    /// mark price, and unrealized PnL.
    /// </param>
    /// <returns>
    /// A <see cref="Position"/> with normalized (absolute) quantity, derived
    /// side (Long/Short), and prices mapped from the Binance model.
    /// </returns>
    public static Position ToLocalPosition(BinancePositionDetailsUsdt pos)
    {
        // Store quantity as an absolute value; the sign is used only to determine direction
        var quantity = Math.Abs(pos.Quantity);

        // Positive quantity = Long (bought), Negative quantity = Short (sold)
        var side = pos.Quantity >= 0 ? PositionSide.Long : PositionSide.Short;

        return new Position
        {
            Exchange = ExchangeName.Binance,
            Symbol = pos.Symbol,
            Side = side,
            Quantity = quantity,
            EntryPrice = pos.EntryPrice,
            // Use Binance's mark price (fair value) as the current price.
            // Mark price is used for PnL calculations and margin/liquidation purposes,
            // and is less susceptible to manipulation than the last traded price.
            CurrentPrice = pos.MarkPrice,
            UnrealizedPnL = pos.UnrealizedPnl
        };
    }
}
