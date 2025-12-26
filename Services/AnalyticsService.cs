using FinDesk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class AnalyticsService
{
    private readonly Db _db;
    public AnalyticsService(Db db) => _db = db;

    public async Task<(decimal income, decimal expense, decimal net)> GetCardsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var txs = await _db.GetTransactionsAsync(fromUtc, toUtc);
        var income = txs.Where(t => t.Amount > 0).Sum(t => t.Amount);
        var expense = txs.Where(t => t.Amount < 0).Sum(t => -t.Amount);
        return (income, expense, income - expense);
    }

    public async Task<Dictionary<MoneyCategory, decimal>> ByCategoryAsync(DateTime fromUtc, DateTime toUtc)
    {
        var txs = await _db.GetTransactionsAsync(fromUtc, toUtc);
        return txs.Where(t => t.Amount < 0)
                  .GroupBy(t => t.Category)
                  .ToDictionary(g => g.Key, g => g.Sum(x => -x.Amount));
    }

    public async Task<Dictionary<DateTime, decimal>> ExpenseByDayAsync(DateTime fromUtc, DateTime toUtc)
    {
        var txs = await _db.GetTransactionsAsync(fromUtc, toUtc);
        return txs.Where(t => t.Amount < 0)
                  .GroupBy(t => t.DateUtc.Date)
                  .OrderBy(g => g.Key)
                  .ToDictionary(g => g.Key, g => g.Sum(x => -x.Amount));
    }
}
