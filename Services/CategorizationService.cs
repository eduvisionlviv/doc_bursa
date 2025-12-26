using FinDesk.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FinDesk.Services;

public sealed class CategorizationService
{
    private readonly Db _db;

    // Basic “smart” rules: fast, explainable, good-enough. Users’ corrections are learned into DB.
    private readonly (Regex rx, MoneyCategory cat)[] _rules = new[]
    {
        (new Regex(@"salary|зарп|avans|аван", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Income),
        (new Regex(@"refund|повернен", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Income),
        (new Regex(@"коміс|fee|commission", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Fees),
        (new Regex(@"tax|подат", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Taxes),

        (new Regex(@"silpo|атб|novus|auchan|metro|fozzy|grocery|market|супермар", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Groceries),
        (new Regex(@"coffee|кафе|restaurant|піца|kfc|mcdonald|sushi|bar|кав", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Cafes),

        (new Regex(@"uber|bolt|taxi|metro|bus|tram|train|ticket|укрзаліз|transport", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Transport),
        (new Regex(@"wog|okko|shell|fuel|azs|азс|benz", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Fuel),

        (new Regex(@"apteka|pharm|doctor|clinic|стомат|лікар|health", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Health),
        (new Regex(@"netflix|spotify|youtube|google one|apple.com/bill|subscription|підписк", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Subscriptions),

        (new Regex(@"rozetka|aliexpress|prom.ua|epicentr|ikea|shopping|магазин", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Shopping),
        (new Regex(@"cinema|movie|game|steam|psn|xbox|entertain", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Entertainment),

        (new Regex(@"rent|оренд|utility|комун|kyivstar|lifecell|vodafone|internet|оплат", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Utilities),
    };

    public CategorizationService(Db db) => _db = db;

    public string NormalizeMerchant(string s)
        => (s ?? "").Trim().ToLowerInvariant();

    public async Task<MoneyCategory> GuessAsync(Transaction t)
    {
        // 1) User learned mapping (strongest)
        var norm = NormalizeMerchant(t.Merchant.Length > 0 ? t.Merchant : t.Description);
        var learned = await _db.TryGetMerchantCategoryAsync(norm);
        if (learned is not null) return learned.Value;

        // 2) Income/Transfers by sign + keywords
        if (t.Amount > 0) return MoneyCategory.Income;

        // 3) Regex rules
        var text = $"{t.Merchant} {t.Description}".Trim();
        foreach (var (rx, cat) in _rules)
            if (rx.IsMatch(text)) return cat;

        return MoneyCategory.Unsorted;
    }

    public IEnumerable<MoneyCategory> AllCategories()
        => (MoneyCategory[])Enum.GetValues(typeof(MoneyCategory));
}
