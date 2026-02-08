using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.Accounting;

public interface IAccountingService
{
    Task RecordTradeAsync(TradeRecord trade);
    void UpdateBalance(ExchangeName exchange, AccountBalance balance);
    PnLSnapshot GetLocalPnLSnapshot(ExchangeName exchange);
    PnLSnapshot? GetExchangePnLSnapshot(ExchangeName exchange);
    (PnLSnapshot Local, PnLSnapshot? Exchange, bool Diverged) GetReconciliationReport(ExchangeName exchange);
    Task<IReadOnlyList<TradeRecord>> GetTradeHistoryAsync(ExchangeName? exchange = null, string? symbol = null, int? limit = null);
}
