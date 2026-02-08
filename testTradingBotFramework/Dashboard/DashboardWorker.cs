// -----------------------------------------------------------------------
// DashboardWorker.cs
//
// Background hosted service that drives the live console dashboard.
// Uses Spectre.Console's Live display to perform flicker-free, in-place
// updates of the terminal output. The render loop runs at a configurable
// interval (DashboardRefreshIntervalMs, default 1500 ms) and delegates
// all layout/content building to DashboardRenderer.
//
// Startup sequence:
//   1. Log the configured refresh interval.
//   2. Wait 2 seconds so other workers (price monitors, position sync, etc.)
//      have time to initialize and populate data.
//   3. Enter the Live display loop, re-rendering on each tick.
//
// Render errors are caught and logged without crashing the dashboard,
// ensuring the UI stays alive even if a single frame fails.
// -----------------------------------------------------------------------

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;
using testTradingBotFramework.Configuration;

namespace testTradingBotFramework.Dashboard;

/// <summary>
/// A <see cref="BackgroundService"/> that continuously refreshes a rich console
/// dashboard by calling <see cref="DashboardRenderer.Render"/> inside a
/// Spectre.Console Live display context. Registered as a hosted service via DI.
/// </summary>
public class DashboardWorker : BackgroundService
{
    /// <summary>Renderer responsible for building the complete dashboard layout each tick.</summary>
    private readonly DashboardRenderer _renderer;

    /// <summary>Trading configuration containing the dashboard refresh interval.</summary>
    private readonly TradingSettings _settings;

    /// <summary>Logger for startup messages and render-error diagnostics.</summary>
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
