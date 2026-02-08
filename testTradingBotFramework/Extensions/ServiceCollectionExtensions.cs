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

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTradingBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<BinanceSettings>(configuration.GetSection(BinanceSettings.SectionName));
        services.Configure<OandaSettings>(configuration.GetSection(OandaSettings.SectionName));
        services.Configure<EventHubSettings>(configuration.GetSection(EventHubSettings.SectionName));
        services.Configure<TradingSettings>(configuration.GetSection(TradingSettings.SectionName));

        // Binance clients
        services.AddSingleton<IBinanceRestClient>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<BinanceSettings>>().Value;
            var client = new BinanceRestClient(options =>
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

        // Oanda clients
        services.AddHttpClient<OandaApiClient>();

        // Exchange clients (keyed DI)
        services.AddKeyedSingleton<IExchangeClient, BinanceExchangeClient>(ExchangeName.Binance);
        services.AddKeyedSingleton<IExchangeClient, OandaExchangeClient>(ExchangeName.Oanda);
        services.AddSingleton<IExchangeFactory, ExchangeFactory>();

        // Price monitors (keyed DI)
        services.AddKeyedSingleton<IPriceMonitor, BinancePriceMonitor>(ExchangeName.Binance);
        services.AddKeyedSingleton<IPriceMonitor, OandaPriceMonitor>(ExchangeName.Oanda);

        // Core services
        services.AddSingleton<IPositionManager, PositionManager>();
        services.AddSingleton<IPositionSizer, FixedFractionPositionSizer>();
        services.AddSingleton<ITradeHistoryStore, InMemoryTradeHistoryStore>();
        services.AddSingleton<IAccountingService, AccountingService>();
        services.AddSingleton<IOrderManager, OrderManager>();
        services.AddSingleton<ISignalParser, SignalParser>();
        services.AddSingleton<ISignalDispatcher, SignalDispatcher>();

        // Dashboard
        services.AddSingleton<DashboardRenderer>(sp =>
        {
            var positionManager = sp.GetRequiredService<IPositionManager>();
            var accountingService = sp.GetRequiredService<IAccountingService>();

            var priceMonitors = Enum.GetValues<ExchangeName>()
                .Select(e => new KeyValuePair<ExchangeName, IPriceMonitor>(
                    e, sp.GetRequiredKeyedService<IPriceMonitor>(e)))
                .ToList();

            return new DashboardRenderer(positionManager, accountingService, priceMonitors);
        });

        // Background workers
        services.AddHostedService<TradingBotWorker>();
        services.AddHostedService<EventHubListenerService>();
        services.AddHostedService<PriceMonitorWorker>();
        services.AddHostedService<PositionSyncWorker>();
        services.AddHostedService<AccountSyncWorker>();
        services.AddHostedService<DashboardWorker>();

        return services;
    }
}
