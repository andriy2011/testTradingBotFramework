// =============================================================================
// ISignalParser.cs
// Abstraction for parsing raw JSON messages from Azure Event Hub into strongly-
// typed TradeSignal objects. Returns null for invalid or incomplete messages
// so the caller can safely skip them without crashing the event processing loop.
//
// Implemented by: SignalParser (uses System.Text.Json with JsonStringEnumConverter)
// =============================================================================

using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.EventProcessing;

/// <summary>
/// Parses raw string messages (typically JSON from Azure Event Hub) into
/// <see cref="TradeSignal"/> objects for downstream processing.
/// </summary>
public interface ISignalParser
{
    /// <summary>
    /// Attempts to parse a raw message into a <see cref="TradeSignal"/>.
    /// Returns <c>null</c> if the message is malformed, empty, or missing required fields.
    /// </summary>
    TradeSignal? Parse(string rawMessage);
}
