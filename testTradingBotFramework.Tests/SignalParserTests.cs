// =============================================================================
// SignalParserTests.cs
// Unit tests for the SignalParser service, which deserializes raw JSON messages
// from Azure Event Hub into strongly-typed TradeSignal objects.
// The parser uses System.Text.Json with JsonStringEnumConverter so that enum
// values (Exchange, Side, OrderType, etc.) arrive as human-readable strings.
//
// Key behaviors under test:
//   - Happy path: valid JSON with all fields parses into a correct TradeSignal
//   - Enum conversion: string enum values ("Binance", "Sell") map to C# enums
//   - Error handling: malformed JSON, missing required fields, and empty input
//     all return null rather than throwing exceptions
// =============================================================================

using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.EventProcessing;

namespace testTradingBotFramework.Tests;

/// <summary>
/// Tests for <see cref="SignalParser"/>, which converts raw JSON strings into
/// <see cref="testTradingBotFramework.Models.TradeSignal"/> objects.
/// The parser is the first step in the signal processing pipeline:
///   Event Hub message (string) -> SignalParser.Parse() -> TradeSignal -> OrderManager
/// </summary>
public class SignalParserTests
{
    // _sut = "System Under Test" — the concrete SignalParser instance we are testing
    private readonly SignalParser _sut;

    public SignalParserTests()
    {
        // ILogger is mocked because we don't need to verify log output here;
        // we only care about the return value of Parse().
        var logger = Substitute.For<ILogger<SignalParser>>();
        _sut = new SignalParser(logger);
    }

    /// <summary>
    /// Verifies that a well-formed JSON payload with all required and optional
    /// fields is correctly deserialized into a TradeSignal with matching values.
    /// This is the primary "happy path" for the Binance crypto futures use case.
    /// </summary>
    [Fact]
    public void Parse_ValidJson_ReturnsTradeSignal()
    {
        // Arrange: JSON payload mimicking what TradingView or an external service
        // would send through Azure Event Hub for a Binance buy signal
        var json = """
        {
            "Exchange": "Binance",
            "Symbol": "BTCUSDT",
            "Action": "Open",
            "Side": "Buy",
            "OrderType": "Market",
            "AssetClass": "CryptoFutures",
            "Quantity": 0.5
        }
        """;

        // Act
        var result = _sut.Parse(json);

        // Assert: every field should be mapped correctly from JSON to the model
        result.Should().NotBeNull();
        result!.Exchange.Should().Be(ExchangeName.Binance);
        result.Symbol.Should().Be("BTCUSDT");
        result.Side.Should().Be(OrderSide.Buy);
        result.OrderType.Should().Be(OrderType.Market);
        result.AssetClass.Should().Be(AssetClass.CryptoFutures);
        result.Quantity.Should().Be(0.5m);
    }

    /// <summary>
    /// Verifies that the JsonStringEnumConverter correctly handles a different
    /// set of enum values (Oanda/Forex/Sell/Limit) — ensuring the converter
    /// works across the full range of supported enum members, not just one combination.
    /// </summary>
    [Fact]
    public void Parse_ValidJson_ConvertsEnumsFromStrings()
    {
        // Arrange: Oanda forex limit order — a different exchange & asset class
        var json = """
        {
            "Exchange": "Oanda",
            "Symbol": "EUR_USD",
            "Action": "Open",
            "Side": "Sell",
            "OrderType": "Limit",
            "AssetClass": "Forex",
            "Price": 1.1050
        }
        """;

        // Act
        var result = _sut.Parse(json);

        // Assert: verify all enum fields were parsed from their string representations
        result.Should().NotBeNull();
        result!.Exchange.Should().Be(ExchangeName.Oanda);
        result.Side.Should().Be(OrderSide.Sell);
        result.OrderType.Should().Be(OrderType.Limit);
        result.AssetClass.Should().Be(AssetClass.Forex);
    }

    /// <summary>
    /// Verifies that malformed JSON (not valid JSON syntax) is caught by the
    /// JsonException handler and returns null instead of propagating the exception.
    /// This is critical because Event Hub messages may be corrupted or from
    /// unexpected producers.
    /// </summary>
    [Fact]
    public void Parse_MalformedJson_ReturnsNull()
    {
        var result = _sut.Parse("not valid json {{{");

        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a JSON object which is technically valid but is missing
    /// the required "Symbol" field returns null. The parser explicitly checks
    /// for a non-empty Symbol after deserialization because a signal without
    /// a symbol cannot be routed to any exchange.
    /// </summary>
    [Fact]
    public void Parse_MissingSymbol_ReturnsNull()
    {
        // Arrange: valid JSON structure, but no "Symbol" field present
        var json = """
        {
            "Exchange": "Binance",
            "Action": "Open",
            "Side": "Buy"
        }
        """;

        var result = _sut.Parse(json);

        // The parser validates that Symbol is present and non-empty
        result.Should().BeNull();
    }

    /// <summary>
    /// Verifies that an empty string input is handled gracefully (returns null).
    /// This can happen if an Event Hub message body is empty due to a producer bug.
    /// System.Text.Json throws a JsonException for empty strings, which the parser catches.
    /// </summary>
    [Fact]
    public void Parse_EmptyString_ReturnsNull()
    {
        var result = _sut.Parse("");

        result.Should().BeNull();
    }
}
