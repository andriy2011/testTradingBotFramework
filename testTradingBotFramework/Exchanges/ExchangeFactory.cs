// ============================================================================
// ExchangeFactory.cs
//
// Concrete implementation of IExchangeFactory that delegates exchange client
// resolution to .NET 8's keyed dependency injection (keyed services).
//
// Keyed DI registration mapping:
//   ExchangeName.Binance  ->  BinanceExchangeClient
//   ExchangeName.Oanda    ->  OandaExchangeClient
//
// This factory is registered as a singleton/scoped service in the DI
// container. When a strategy or service needs to interact with a specific
// exchange, it calls GetClient(ExchangeName) and receives the correctly
// configured client instance without needing to know about the DI container.
// ============================================================================

using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Exchanges;

/// <summary>
/// Resolves <see cref="IExchangeClient"/> implementations using .NET 8
/// keyed dependency injection. Acts as a thin wrapper around
/// <see cref="IServiceProvider.GetRequiredKeyedService{T}"/> to keep
/// exchange resolution logic centralized and testable.
/// </summary>
public class ExchangeFactory : IExchangeFactory
{
    /// <summary>
    /// The DI service provider used to resolve keyed exchange client registrations.
    /// </summary>
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="ExchangeFactory"/>.
    /// </summary>
    /// <param name="serviceProvider">
    /// The application's service provider, injected by the DI container.
    /// Must contain keyed registrations for each supported <see cref="ExchangeName"/>.
    /// </param>
    public ExchangeFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Internally calls <c>GetRequiredKeyedService&lt;IExchangeClient&gt;(exchange)</c>,
    /// which will throw <see cref="InvalidOperationException"/> if no client is
    /// registered for the given <paramref name="exchange"/> key.
    /// </remarks>
    public IExchangeClient GetClient(ExchangeName exchange)
    {
        // Resolve the IExchangeClient keyed by the ExchangeName enum value.
        // Each exchange implementation is registered with its corresponding key
        // during service configuration (e.g., services.AddKeyedSingleton<IExchangeClient, BinanceExchangeClient>(ExchangeName.Binance)).
        return _serviceProvider.GetRequiredKeyedService<IExchangeClient>(exchange);
    }
}
