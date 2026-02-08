using testTradingBotFramework.Extensions;

var builder = Host.CreateDefaultBuilder(args)
    .UseTradingBotSerilog()
    .ConfigureServices((context, services) =>
    {
        services.AddTradingBotServices(context.Configuration);
    });

var host = builder.Build();
await host.RunAsync();
