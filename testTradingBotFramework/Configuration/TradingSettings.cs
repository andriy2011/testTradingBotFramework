// ============================================================================
// File: TradingSettings.cs
// Purpose: Main trading behavior and risk management configuration.
// Binding: Bound from the "Trading" section of appsettings.json via
//          IOptions<TradingSettings> in the DI container.
// Notes: DryRunMode defaults to true so that new deployments simulate
//        order execution without placing real trades. This is the primary
//        safety switch for the entire trading bot.
// ============================================================================

namespace testTradingBotFramework.Configuration;

/// <summary>
/// Central configuration for trading behavior, risk limits, and background
/// worker timing. Controls whether the bot operates in dry-run (simulated)
/// mode, how large each position can be relative to account equity, the
/// maximum number of concurrent positions, and the cadence of periodic
/// synchronization tasks.
/// </summary>
public class TradingSettings
{
    /// <summary>
    /// The configuration section name used to bind this settings class.
    /// Referenced during service registration (e.g.,
    /// <c>builder.Services.Configure&lt;TradingSettings&gt;(config.GetSection(TradingSettings.SectionName))</c>).
    /// </summary>
    public const string SectionName = "Trading";

    /// <summary>
    /// When <c>true</c>, the bot logs intended orders but does not submit them
    /// to the exchange. This is the primary safety switch for the entire system.
    /// Defaults to <c>true</c> to prevent accidental real-money trades during
    /// development, testing, or initial deployment.
    /// </summary>
    public bool DryRunMode { get; set; } = true;

    /// <summary>
    /// The maximum position size as a percentage of total account equity,
    /// used by the fixed-fraction position sizer. For example, a value of
    /// <c>2.0</c> means each new position can risk at most 2% of the account.
    /// This is a core risk-management parameter.
    /// </summary>
    public decimal MaxPositionSizePercent { get; set; } = 2.0m;

    /// <summary>
    /// The maximum number of open positions allowed at any given time.
    /// New trade signals are rejected once this limit is reached.
    /// Acts as a portfolio-level risk cap to prevent over-exposure.
    /// </summary>
    public int MaxOpenPositions { get; set; } = 10;

    /// <summary>
    /// The P&amp;L divergence threshold (as a percentage) used during position
    /// reconciliation. When the locally tracked P&amp;L deviates from the
    /// broker-reported P&amp;L by more than this value, a reconciliation
    /// warning is raised. Helps detect desync between local state and the
    /// exchange.
    /// </summary>
    public decimal ReconciliationThreshold { get; set; } = 1.0m;

    /// <summary>
    /// How often (in seconds) the background position-sync worker polls the
    /// broker to reconcile open positions with local tracking state.
    /// Lower values improve accuracy but increase API request volume.
    /// </summary>
    public int PositionSyncIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// How often (in seconds) the background account-sync worker polls the
    /// broker to refresh account balance, equity, and margin information.
    /// </summary>
    public int AccountSyncIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// How often (in milliseconds) the real-time console dashboard refreshes
    /// its display. Controls the UI update rate for the terminal-based
    /// monitoring view. A value of <c>1500</c> means roughly 0.67 updates
    /// per second.
    /// </summary>
    public int DashboardRefreshIntervalMs { get; set; } = 1500;
}
