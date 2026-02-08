// ============================================================================
// Program.cs - Application Entry Point
// ============================================================================
// This is the entry point for the multi-exchange trading bot, built as a
// .NET Worker Service (long-running background service). The application
// connects to multiple exchanges (Binance, Oanda) to monitor prices,
// manage positions, execute orders, and process trading signals.
//
// Host.CreateDefaultBuilder provides the following out of the box:
//   - Loading configuration from appsettings.json and appsettings.{Environment}.json
//   - Loading configuration from environment variables and command-line args
//   - Configuring default logging providers (Console, Debug, EventSource)
//   - Setting the content root to the current directory
//   - Enabling dependency injection via IServiceCollection
// ============================================================================

using testTradingBotFramework.Extensions;

// Build the generic host with Serilog logging and all trading bot services.
var builder = Host.CreateDefaultBuilder(args)
    // UseTradingBotSerilog() replaces the default .NET logging with Serilog,
    // configuring dual output (rolling daily log files + console) and enriching
    // every log entry with application context metadata.
    .UseTradingBotSerilog()
    .ConfigureServices((context, services) =>
    {
        // AddTradingBotServices() is the DI composition root that registers all
        // configuration bindings, exchange clients (Binance REST/WebSocket, Oanda HTTP),
        // core business services (position management, order management, accounting),
        // and background workers (price monitoring, event hub listener, dashboard).
        services.AddTradingBotServices(context.Configuration);
    });

// Build the fully configured host and run it asynchronously.
// RunAsync blocks until the host is shut down (e.g., via Ctrl+C or SIGTERM),
// keeping all registered IHostedService background workers alive.
var host = builder.Build();
await host.RunAsync();
