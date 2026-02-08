// ============================================================================
// ServiceCollectionExtensions.cs - Dependency Injection Composition Root
// ============================================================================
// This file serves as the DI composition root for the entire trading bot
// application. It is the single location where all services, clients, and
// background workers are registered into the .NET dependency injection
// container. The registrations are organized into logical sections:
//
//   1. Configuration    - IOptions<T> bindings from appsettings.json sections
//   2. Binance Clients  - REST and WebSocket clients with testnet support
//   3. Oanda Clients    - Typed HttpClient for the Oanda REST API
//   4. Exchange Clients  - Keyed DI for multi-exchange abstraction (IExchangeClient)
//   5. Price Monitors   - Keyed DI for exchange-specific price feeds (IPriceMonitor)
//   6. Core Services    - Singleton business logic (positions, orders, accounting)
//   7. Dashboard        - Manual factory registration for keyed service injection
//   8. Background Workers - Six IHostedService implementations that run concurrently
//
// All services are registered as singletons because the trading bot maintains
// long-lived state (open positions, price caches, account balances) that must
// be shared across background workers throughout the application lifetime.
// ============================================================================

using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Dashboard;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Exchanges.Binance;
using testTradingBotFramework.Exchanges.Oanda;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.EventProcessing;
using testTradingBotFramework.Services.OrderManagement;
using testTradingBotFramework.Services.PositionManagement;
using testTradingBotFramework.Services.PriceMonitoring;
using testTradingBotFramework.Services.PriceMonitoring.Binance;
using testTradingBotFramework.Services.PriceMonitoring.Oanda;
using testTradingBotFramework.Workers;

namespace testTradingBotFramework.Extensions;

/// <summary>
/// Provides extension methods on <see cref="IServiceCollection"/> to register
/// all trading bot services, exchange clients, and background workers into
/// the dependency injection container. This class acts as the composition root
/// for the entire application.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all trading bot services into the DI container, including
    /// configuration bindings, exchange clients (Binance, Oanda), core business
    /// services, the dashboard renderer, and all background worker processes.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configuration">
    /// The application configuration (typically from appsettings.json and environment
    /// variables) used to bind strongly-typed settings objects.
    /// </param>
    /// <returns>The same <see cref="IServiceCollection"/> instance for fluent chaining.</returns>
    public static IServiceCollection AddTradingBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // ---------------------------------------------------------------
        // 1. Configuration - IOptions<T> pattern
        // ---------------------------------------------------------------
        // Bind each settings section from appsettings.json to a strongly-typed
        // POCO class. These are injected elsewhere as IOptions<T> or
        // IOptionsMonitor<T>, enabling hot-reload of configuration values
        // without restarting the application.
        services.Configure<BinanceSettings>(configuration.GetSection(BinanceSettings.SectionName));
        services.Configure<OandaSettings>(configuration.GetSection(OandaSettings.SectionName));
        services.Configure<EventHubSettings>(configuration.GetSection(EventHubSettings.SectionName));
        services.Configure<TradingSettings>(configuration.GetSection(TradingSettings.SectionName));

        // ---------------------------------------------------------------
        // 2. Binance Clients - REST and WebSocket with testnet support
        // ---------------------------------------------------------------
        // The Binance REST client is used for request/response operations such as
        // placing orders, fetching account balances, and querying trade history.
        // It is registered as a singleton with a factory that configures API
        // credentials and optionally switches to the Binance testnet environment
        // for paper trading and development.
        services.AddSingleton<IBinanceRestClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BinanceSettings>>().Value;
            var client = new BinanceRestClient(options =>
            {
                // Only set API credentials if an API key is configured;
                // allows running in read-only/public-data mode without keys.
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    options.ApiCredentials = new ApiCredentials(settings.ApiKey, settings.ApiSecret);
                }
                // When UseTestnet is true, all requests are routed to the Binance
                // testnet (testnet.binance.vision) instead of the production API,
                // enabling safe testing with simulated funds.
                if (settings.UseTestnet)
                {
                    options.Environment = BinanceEnvironment.Testnet;
                }
            });
            return client;
        });

        // The Binance WebSocket client maintains persistent connections for
        // real-time streaming data such as live price tickers, order book updates,
        // and user account/trade event streams. Uses the same credential and
        // testnet configuration as the REST client above.
        services.AddSingleton<IBinanceSocketClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BinanceSettings>>().Value;
            var client = new BinanceSocketClient(options =>
            {
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    options.ApiCredentials = new ApiCredentials(settings.ApiKey, settings.ApiSecret);
                }
                if (settings.UseTestnet)
                {
                    options.Environment = BinanceEnvironment.Testnet;
                }
            });
            return client;
        });

        // ---------------------------------------------------------------
        // 3. Oanda Client - Typed HttpClient
        // ---------------------------------------------------------------
        // Registers OandaApiClient as a typed HttpClient. The .NET HttpClientFactory
        // manages the underlying HttpMessageHandler lifetime, preventing socket
        // exhaustion while allowing the OandaApiClient to configure base addresses,
        // authentication headers, and request defaults in its constructor.
        services.AddHttpClient<OandaApiClient>();

        // ---------------------------------------------------------------
        // 4. Exchange Clients - Keyed DI for multi-exchange resolution
        // ---------------------------------------------------------------
        // Keyed DI (introduced in .NET 8) allows multiple implementations of the
        // same interface to coexist in the container, differentiated by an enum key.
        // When a service needs to interact with a specific exchange, it requests
        // IExchangeClient keyed by ExchangeName.Binance or ExchangeName.Oanda.
        // The ExchangeFactory wraps this pattern, providing a clean API to resolve
        // the correct exchange client at runtime based on a trading signal's target.
        services.AddKeyedSingleton<IExchangeClient, BinanceExchangeClient>(ExchangeName.Binance);
        services.AddKeyedSingleton<IExchangeClient, OandaExchangeClient>(ExchangeName.Oanda);
        services.AddSingleton<IExchangeFactory, ExchangeFactory>();

        // ---------------------------------------------------------------
        // 5. Price Monitors - Keyed DI for exchange-specific price feeds
        // ---------------------------------------------------------------
        // Each exchange has its own price monitoring implementation that subscribes
        // to real-time price data using the exchange's native protocol (WebSocket
        // for Binance, polling/streaming for Oanda). Keyed by ExchangeName so
        // workers can resolve the correct monitor for each configured exchange.
        services.AddKeyedSingleton<IPriceMonitor, BinancePriceMonitor>(ExchangeName.Binance);
        services.AddKeyedSingleton<IPriceMonitor, OandaPriceMonitor>(ExchangeName.Oanda);

        // ---------------------------------------------------------------
        // 6. Core Business Services - Singletons with shared state
        // ---------------------------------------------------------------
        // These services implement the trading bot's core business logic.
        // All are singletons because they maintain in-memory state that must
        // be consistent across all background workers:
        services.AddSingleton<IPositionManager, PositionManager>();           // Tracks open/closed positions across all exchanges
        services.AddSingleton<IPositionSizer, FixedFractionPositionSizer>();  // Calculates position sizes using fixed-fraction risk model
        services.AddSingleton<ITradeHistoryStore, InMemoryTradeHistoryStore>();// Stores completed trade records in memory for reporting
        services.AddSingleton<IAccountingService, AccountingService>();       // Tracks account balances, P&L, and equity across exchanges
        services.AddSingleton<IOrderManager, OrderManager>();                 // Validates and submits orders to the appropriate exchange
        services.AddSingleton<ISignalParser, SignalParser>();                 // Parses raw trading signals into structured signal objects
        services.AddSingleton<ISignalDispatcher, SignalDispatcher>();         // Routes parsed signals to the correct exchange and order flow

        // ---------------------------------------------------------------
        // 7. Dashboard - Manual factory for keyed service injection
        // ---------------------------------------------------------------
        // DashboardRenderer requires all IPriceMonitor instances (one per exchange)
        // to display live prices. Since keyed services cannot be auto-injected as
        // a collection, we use a manual factory delegate that iterates over all
        // ExchangeName enum values and resolves each keyed IPriceMonitor explicitly.
        services.AddSingleton<DashboardRenderer>(sp =>
        {
            var positionManager = sp.GetRequiredService<IPositionManager>();
            var accountingService = sp.GetRequiredService<IAccountingService>();

            // Build a list of all exchange-to-price-monitor mappings by resolving
            // each keyed IPriceMonitor from the container using the ExchangeName enum.
            var priceMonitors = Enum.GetValues<ExchangeName>()
                .Select(e => new KeyValuePair<ExchangeName, IPriceMonitor>(
                    e, sp.GetRequiredKeyedService<IPriceMonitor>(e)))
                .ToList();

            return new DashboardRenderer(positionManager, accountingService, priceMonitors);
        });

        // ---------------------------------------------------------------
        // 8. Background Workers (IHostedService implementations)
        // ---------------------------------------------------------------
        // These six workers run concurrently as long-running background tasks
        // managed by the .NET Generic Host. Each is started when the host starts
        // and gracefully stopped on shutdown (Ctrl+C / SIGTERM).
        services.AddHostedService<TradingBotWorker>();        // Main orchestrator: coordinates signal processing and trade execution
        services.AddHostedService<EventHubListenerService>(); // Listens for incoming trading signals from an external event hub
        services.AddHostedService<PriceMonitorWorker>();      // Starts and manages real-time price feeds for all configured exchanges
        services.AddHostedService<PositionSyncWorker>();      // Periodically syncs local position state with exchange-reported positions
        services.AddHostedService<AccountSyncWorker>();       // Periodically syncs account balances and equity from each exchange
        services.AddHostedService<DashboardWorker>();         // Periodically renders a live console dashboard showing positions and P&L

        return services;
    }
}
