using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using testTradingBotFramework.Configuration;

namespace testTradingBotFramework.Dashboard;

public class DashboardWorker : BackgroundService
{
    private readonly DashboardRenderer _renderer;
    private readonly TradingSettings _settings;
    private readonly ILogger<DashboardWorker> _logger;

    public DashboardWorker(
        DashboardRenderer renderer,
        IOptions<TradingSettings> settings,
        ILogger<DashboardWorker> logger)
    {
        _renderer = renderer;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DashboardWorker starting. Refresh interval: {Interval}ms", _settings.DashboardRefreshIntervalMs);

        // Small delay to let other workers initialize
        await Task.Delay(2000, stoppingToken);

        await AnsiConsole.Live(new Text("Initializing dashboard..."))
            .StartAsync(async ctx =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var display = _renderer.Render();
                        ctx.UpdateTarget(display);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Dashboard render error");
                    }

                    await Task.Delay(_settings.DashboardRefreshIntervalMs, stoppingToken);
                }
            });
    }
}
