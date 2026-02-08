using Serilog;
using Serilog.Events;

namespace testTradingBotFramework.Extensions;

public static class LoggingExtensions
{
    public static IHostBuilder UseTradingBotSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "TradingBot")
                .WriteTo.File(
                    path: "logs/tradingbot-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.Console(
                    restrictedToMinimumLevel: LogEventLevel.Information,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
        });
    }
}
