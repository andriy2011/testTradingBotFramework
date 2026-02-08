// -----------------------------------------------------------------------
// <file>
//   SignalAction.cs - Action requested by a trade signal.
//   Defines the operation to perform when a signal is received.
// </file>
// -----------------------------------------------------------------------

namespace testTradingBotFramework.Models.Enums;

/// <summary>
/// Action requested by a trade signal, defining the operation to perform
/// when the signal is processed.
/// </summary>
/// <remarks>
/// Currently only supports <see cref="Open"/>. Future values could include
/// Close, ModifyStopLoss, TakeProfit, and other position management actions.
/// </remarks>
public enum SignalAction
{
    /// <summary>
    /// Place a new order to open or add to a position.
    /// </summary>
    Open
}
