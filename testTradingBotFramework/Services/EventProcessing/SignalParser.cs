// =============================================================================
// SignalParser.cs
// Deserializes raw JSON messages (received from Azure Event Hub) into
// strongly-typed TradeSignal objects. This is the entry point of the signal
// processing pipeline: raw string -> TradeSignal -> SignalDispatcher -> OrderManager.
//
// Uses System.Text.Json with:
//   - PropertyNameCaseInsensitive: tolerates different casing from producers
//   - JsonStringEnumConverter: allows enum values to be sent as strings
//     (e.g., "Binance", "Buy", "Market") rather than integer values
//
// Validation: rejects signals with missing or empty Symbol fields, since
// a signal without a symbol cannot be routed to any exchange.
// =============================================================================

using System.Text.Json;
using Microsoft.Extensions.Logging;
using testTradingBotFramework.Models;

namespace testTradingBotFramework.Services.EventProcessing;

/// <summary>
/// Parses raw JSON strings into <see cref="TradeSignal"/> objects.
/// Returns null for invalid input rather than throwing exceptions,
/// allowing the Event Hub listener to skip bad messages and continue processing.
/// </summary>
public class SignalParser : ISignalParser
{
    private readonly ILogger<SignalParser> _logger;

    /// <summary>
    /// Shared, thread-safe JSON serializer options configured with:
    /// - Case-insensitive property matching (handles different JSON casing conventions)
    /// - String-to-enum converter (allows "Buy" instead of 0 in JSON payloads)
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public SignalParser(ILogger<SignalParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to parse a raw JSON string into a <see cref="TradeSignal"/>.
    /// </summary>
    /// <param name="rawMessage">The raw JSON string from Azure Event Hub.</param>
    /// <returns>
    /// A populated <see cref="TradeSignal"/> on success, or null if:
    /// - The JSON is malformed (caught as JsonException)
    /// - Deserialization returns null (e.g., JSON is literally "null")
    /// - The required Symbol field is missing or whitespace
    /// </returns>
    public TradeSignal? Parse(string rawMessage)
    {
        try
        {
            // Attempt JSON deserialization with case-insensitive matching and enum conversion
            var signal = JsonSerializer.Deserialize<TradeSignal>(rawMessage, JsonOptions);
            if (signal is null)
            {
                _logger.LogWarning("Deserialized signal was null from message: {Message}", rawMessage);
                return null;
            }

            // Validate the required Symbol field — a signal without a symbol
            // cannot be routed to any exchange instrument
            if (string.IsNullOrWhiteSpace(signal.Symbol))
            {
                _logger.LogWarning("Signal missing required Symbol field: {Message}", rawMessage);
                return null;
            }

            _logger.LogDebug("Parsed signal {SignalId} for {Exchange}:{Symbol} ({Action} {Side})",
                signal.SignalId, signal.Exchange, signal.Symbol, signal.Action, signal.Side);

            return signal;
        }
        catch (JsonException ex)
        {
            // Catch malformed JSON gracefully — log the error and return null
            // so the Event Hub listener can skip this message and continue
            _logger.LogError(ex, "Failed to parse trade signal from message: {Message}", rawMessage);
            return null;
        }
    }
}
