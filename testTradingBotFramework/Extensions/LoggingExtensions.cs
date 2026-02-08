// ============================================================================
// LoggingExtensions.cs - Serilog Logging Configuration
// ============================================================================
// Configures structured logging via Serilog for the trading bot application.
// Serilog replaces the default .NET logging infrastructure, providing:
//
//   - Dual output sinks: rolling daily log files for persistent storage and
//     console output for real-time monitoring during development/operations.
//   - Structured log enrichment: every log event is tagged with contextual
//     properties (source context, application name) for filtering and analysis.
//   - Configuration-driven overrides: log levels and sink settings can be
//     adjusted at runtime via appsettings.json without recompilation.
//
// Log files are written to the "logs/" directory with a 30-day retention
// policy, automatically cleaning up old files to manage disk space.
// ============================================================================

using Serilog;
using Serilog.Events;

namespace testTradingBotFramework.Extensions;

/// <summary>
/// Provides extension methods for configuring Serilog as the logging provider
/// for the trading bot application's <see cref="IHostBuilder"/>.
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Configures the host to use Serilog with dual-sink output (file and console),
    /// structured enrichment, and configuration-driven log level overrides.
    /// This method replaces the default .NET logging providers with Serilog.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure with Serilog.</param>
    /// <returns>The same <see cref="IHostBuilder"/> instance for fluent chaining.</returns>
    public static IHostBuilder UseTradingBotSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, config) =>
        {
            config
                // Allow appsettings.json to override minimum log levels per namespace
                // (e.g., set "Microsoft" to Warning while keeping app logs at Debug).
                .ReadFrom.Configuration(context.Configuration)

                // Enrich each log event with properties pushed onto the LogContext
                // (e.g., correlation IDs, request metadata from using statements).
                .Enrich.FromLogContext()

                // Tag every log event with a static "Application" property so logs
                // can be filtered by application name in centralized logging systems.
                .Enrich.WithProperty("Application", "TradingBot")

                // --- File Sink ---
                // Writes all log levels to rolling daily log files in the "logs/" directory.
                // Files are named "tradingbot-YYYYMMDD.log" via the RollingInterval.Day setting.
                // Old log files are automatically deleted after 30 days (retainedFileCountLimit).
                // The output template includes full timestamps with timezone, log level,
                // source context (class name), and exception details when present.
                .WriteTo.File(
                    path: "logs/tradingbot-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")

                // --- Console Sink ---
                // Writes Information-level and above to the console for real-time monitoring.
                // Uses a shorter timestamp format (HH:mm:ss) to keep console output compact.
                // Debug/Verbose messages are suppressed on the console to reduce noise,
                // but are still captured in the file sink for troubleshooting.
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });
    }
}
