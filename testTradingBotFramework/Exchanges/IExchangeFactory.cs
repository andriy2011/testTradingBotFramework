// ============================================================================
// IExchangeFactory.cs
//
// Factory interface for resolving exchange client instances at runtime.
// This abstraction wraps the keyed DI resolution logic so that callers
// (strategies, services, controllers) do not depend directly on the
// IServiceProvider or the DI container. Instead, they request an
// IExchangeClient by providing an ExchangeName enum value.
//
// This pattern keeps the exchange resolution mechanism pluggable and
// testable -- in unit tests, IExchangeFactory can be easily mocked
// to return a stub or fake exchange client.
// ============================================================================

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges;

/// <summary>
/// Factory interface for obtaining <see cref="IExchangeClient"/> instances
/// based on the target exchange. Abstracts away the DI container so that
/// consuming code remains decoupled from the service resolution mechanism.
/// </summary>
public interface IExchangeFactory
{
    /// <summary>
    /// Resolves and returns the <see cref="IExchangeClient"/> implementation
    /// registered for the specified exchange.
    /// </summary>
    /// <param name="exchange">
    /// The exchange identifier (e.g., <see cref="ExchangeName.Binance"/> or
    /// <see cref="ExchangeName.Oanda"/>) used as the keyed DI lookup key.
    /// </param>
    /// <returns>The concrete exchange client for the requested exchange.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if no client is registered for the given <paramref name="exchange"/> key.
    /// </exception>
    IExchangeClient GetClient(ExchangeName exchange);
}
