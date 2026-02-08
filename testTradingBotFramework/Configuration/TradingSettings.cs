namespace testTradingBotFramework.Configuration;

public class TradingSettings
{
    public const string SectionName = "Trading";

    public bool DryRunMode { get; set; } = true;
    public decimal MaxPositionSizePercent { get; set; } = 2.0m;
    public int MaxOpenPositions { get; set; } = 10;
    public decimal ReconciliationThreshold { get; set; } = 1.0m;
    public int PositionSyncIntervalSeconds { get; set; } = 30;
    public int AccountSyncIntervalSeconds { get; set; } = 60;
    public int DashboardRefreshIntervalMs { get; set; } = 1500;
}
