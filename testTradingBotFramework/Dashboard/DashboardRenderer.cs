// -----------------------------------------------------------------------
// DashboardRenderer.cs
//
// Real-time console dashboard renderer using the Spectre.Console library.
// Produces a 2x2 grid layout (IRenderable) containing four panels:
//   Top-left:     Open Positions  -- all active positions with color-coded P&L
//   Top-right:    Account Summary -- per-exchange P&L and reconciliation status
//   Bottom-left:  Live Prices     -- bid/ask/spread from every price monitor
//   Bottom-right: System Status   -- uptime counter and background worker status
//
// The Render() method is called repeatedly by DashboardWorker, which feeds
// the returned IRenderable into AnsiConsole.Live() for flicker-free updates.
// -----------------------------------------------------------------------

using Spectre.Console;
using Spectre.Console.Rendering;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;
using testTradingBotFramework.Services.PositionManagement;
using testTradingBotFramework.Services.PriceMonitoring;

namespace testTradingBotFramework.Dashboard;

/// <summary>
/// Builds a rich console dashboard composed of four panels arranged in a 2x2 grid.
/// Each panel aggregates data from a different domain service (positions, accounting,
/// price monitoring) and formats it with Spectre.Console markup for colored output.
/// This class is stateless per render call -- it reads live data on every invocation.
/// </summary>
public class DashboardRenderer
{
    /// <summary>Service that tracks all open trading positions across exchanges.</summary>
    private readonly IPositionManager _positionManager;

    /// <summary>Service providing local and exchange-side P&amp;L snapshots plus reconciliation.</summary>
    private readonly IAccountingService _accountingService;

    /// <summary>
    /// Collection of price monitors keyed by exchange name. Each monitor exposes
    /// real-time bid/ask prices for every symbol it tracks on that exchange.
    /// </summary>
    private readonly IEnumerable<KeyValuePair<ExchangeName, IPriceMonitor>> _priceMonitors;

    /// <summary>
    /// Timestamp captured once at construction time, used to calculate the uptime
    /// displayed in the System Status panel.
    /// </summary>
    private readonly DateTimeOffset _startTime = DateTimeOffset.UtcNow;

    public DashboardRenderer(
        IPositionManager positionManager,
        IAccountingService accountingService,
        IEnumerable<KeyValuePair<ExchangeName, IPriceMonitor>> priceMonitors)
    {
        _positionManager = positionManager;
        _accountingService = accountingService;
        _priceMonitors = priceMonitors;
    }

    public IRenderable Render()
    {
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Top").SplitColumns(
                    new Layout("Positions"),
                    new Layout("Account")),
                new Layout("Bottom").SplitColumns(
                    new Layout("Prices"),
                    new Layout("Status")));

        layout["Positions"].Update(BuildPositionsPanel());
        layout["Account"].Update(BuildAccountPanel());
        layout["Prices"].Update(BuildPricesPanel());
        layout["Status"].Update(BuildStatusPanel());

        return layout;
    }

    private Panel BuildPositionsPanel()
    {
        var positions = _positionManager.GetOpenPositions();

        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Exchange")
            .AddColumn("Symbol")
            .AddColumn("Side")
            .AddColumn("Qty")
            .AddColumn("Entry")
            .AddColumn("Current")
            .AddColumn("Unrealized P&L");

        if (positions.Count == 0)
        {
            table.AddRow("--", "--", "--", "--", "--", "--", "--");
        }
        else
        {
            foreach (var pos in positions)
            {
                var pnlColor = pos.UnrealizedPnL >= 0 ? "green" : "red";
                table.AddRow(
                    pos.Exchange.ToString(),
                    pos.Symbol,
                    pos.Side.ToString(),
                    pos.Quantity.ToString("G"),
                    pos.EntryPrice.ToString("F4"),
                    pos.CurrentPrice.ToString("F4"),
                    $"[{pnlColor}]{pos.UnrealizedPnL:F4}[/]");
            }
        }

        return new Panel(table)
            .Header("[bold yellow]Open Positions[/]")
            .Border(BoxBorder.Rounded);
    }

    private Panel BuildAccountPanel()
    {
        var rows = new Rows(new Text(""));

        foreach (var exchange in Enum.GetValues<ExchangeName>())
        {
            var local = _accountingService.GetLocalPnLSnapshot(exchange);
            var exchangeSnap = _accountingService.GetExchangePnLSnapshot(exchange);
            var (_, _, diverged) = _accountingService.GetReconciliationReport(exchange);

            var status = diverged ? "[red]DIVERGED[/]" : "[green]OK[/]";

            rows = new Rows(
                new Markup($"[bold]{exchange}[/]"),
                new Markup($"  Local P&L: {local.NetPnL:F4} ({local.TotalTrades} trades, fees: {local.TotalFees:F4})"),
                new Markup($"  Exchange P&L: {(exchangeSnap is not null ? exchangeSnap.UnrealizedPnL.ToString("F4") : "N/A")}"),
                new Markup($"  Reconciliation: {status}"),
                new Text(""));
        }

        return new Panel(rows)
            .Header("[bold yellow]Account Summary[/]")
            .Border(BoxBorder.Rounded);
    }

    private Panel BuildPricesPanel()
    {
        var table = new Table()
            .Border(TableBorder.Simple)
            .AddColumn("Exchange")
            .AddColumn("Symbol")
            .AddColumn("Bid")
            .AddColumn("Ask")
            .AddColumn("Spread")
            .AddColumn("Updated");

        bool hasData = false;
        foreach (var (exchange, monitor) in _priceMonitors)
        {
            foreach (var (symbol, price) in monitor.GetAllPrices())
            {
                hasData = true;
                var spread = price.Ask - price.Bid;
                var ago = DateTimeOffset.UtcNow - price.Timestamp;
                table.AddRow(
                    exchange.ToString(),
                    symbol,
                    price.Bid.ToString("F5"),
                    price.Ask.ToString("F5"),
                    spread.ToString("F5"),
                    $"{ago.TotalSeconds:F0}s ago");
            }
        }

        if (!hasData)
        {
            table.AddRow("--", "--", "--", "--", "--", "--");
        }

        return new Panel(table)
            .Header("[bold yellow]Live Prices[/]")
            .Border(BoxBorder.Rounded);
    }

    private Panel BuildStatusPanel()
    {
        var uptime = DateTimeOffset.UtcNow - _startTime;

        var content = new Rows(
            new Markup($"[bold]Uptime:[/] {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"),
            new Markup($"[bold]Time:[/] {DateTimeOffset.UtcNow:u}"),
            new Text(""),
            new Markup("[bold]Workers:[/]"),
            new Markup("  EventHubListener: [green]running[/]"),
            new Markup("  PriceMonitor: [green]running[/]"),
            new Markup("  PositionSync: [green]running[/]"),
            new Markup("  AccountSync: [green]running[/]"));

        return new Panel(content)
            .Header("[bold yellow]System Status[/]")
            .Border(BoxBorder.Rounded);
    }
}
