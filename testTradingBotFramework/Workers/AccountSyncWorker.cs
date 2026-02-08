using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using testTradingBotFramework.Configuration;
using testTradingBotFramework.Exchanges;
using testTradingBotFramework.Models.Enums;
using testTradingBotFramework.Services.Accounting;

namespace testTradingBotFramework.Workers;

public class AccountSyncWorker : BackgroundService
{
    private readonly IExchangeFactory _exchangeFactory;
    private readonly IAccountingService _accountingService;
    private readonly TradingSettings _settings;
    private readonly ILogger<AccountSyncWorker> _logger;

    public AccountSyncWorker(
        IExchangeFactory exchangeFactory,
        IAccountingService accountingService,
        IOptions<TradingSettings> settings,
        ILogger<AccountSyncWorker> logger)
    {
        _exchangeFactory = exchangeFactory;
        _accountingService = accountingService;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AccountSyncWorker starting. Interval: {Interval}s", _settings.AccountSyncIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(_settings.AccountSyncIntervalSeconds), stoppingToken);

            foreach (var exchange in Enum.GetValues<ExchangeName>())
            {
                try
                {
                    var client = _exchangeFactory.GetClient(exchange);
                    var balance = await client.GetAccountBalanceAsync(stoppingToken);

                    _accountingService.UpdateBalance(exchange, balance);

                    var (local, exchangePnL, diverged) = _accountingService.GetReconciliationReport(exchange);
                    if (diverged)
                    {
                        _logger.LogWarning(
                            "RECONCILIATION WARNING for {Exchange}: Local PnL={LocalPnL:F4}, Exchange PnL={ExchangePnL:F4}",
                            exchange, local.NetPnL, exchangePnL?.NetPnL);
                    }
                    else
                    {
                        _logger.LogDebug("Account sync OK for {Exchange}: Balance={Balance}", exchange, balance.TotalBalance);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to sync account for {Exchange}", exchange);
                }
            }
        }
    }
}
