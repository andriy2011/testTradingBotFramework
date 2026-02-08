using testTradingBotFramework.Models;
using testTradingBotFramework.Models.Enums;

namespace testTradingBotFramework.Services.Accounting;

public interface ITradeHistoryStore
{
    Task AddAsync(TradeRecord trade);
    IReadOnlyList<TradeRecord> GetAll(ExchangeName? exchange = null);
    Task<IReadOnlyList<TradeRecord>> QueryAsync(ExchangeName? exchange = null, string? symbol = null, int? limit = null);
}
