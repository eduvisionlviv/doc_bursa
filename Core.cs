using CsvHelper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FinDesk;

public enum MoneyCategory
{
    Unsorted = 0,
    Income = 1,
    Transfers = 2,
    Groceries = 10,
    Cafes = 11,
    Transport = 12,
    Fuel = 13,
    Utilities = 15,
    Health = 16,
    Shopping = 17,
    Entertainment = 18,
    Subscriptions = 20,
    Fees = 22,
    Taxes = 23
}

public sealed record PeriodPreset(string Key, string Display);

public sealed class AppSettings
{
    public string DataDir { get; set; } = "";
    public string DbPath { get; set; } = "";
    public string MonoTokenProtected { get; set; } = "";
}

public static class SettingsService
{
    private static string GetAppDir()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FinDesk");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string SettingsPath => Path.Combine(GetAppDir(), "settings.json");

    public static async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            var s = NewDefaults();
            await SaveAsync(s);
            return s;
        }

        var json = await File.ReadAllTextAsync(SettingsPath, Encoding.UTF8);
        var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? NewDefaults();

        if (string.IsNullOrWhiteSpace(settings.DataDir))
            settings.DataDir = GetAppDir();

        if (string.IsNullOrWhiteSpace(settings.DbPath))
            settings.DbPath = Path.Combine(settings.DataDir, "finance.sqlite");

        return settings;
    }

    public static async Task SaveAsync(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(SettingsPath, json, Encoding.UTF8);
    }

    public static string Protect(string plain)
    {
        if (string.IsNullOrWhiteSpace(plain)) return "";
        try
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch { return ""; }
    }

    public static string Unprotect(string protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64)) return "";
        try
        {
            var protectedBytes = Convert.FromBase64String(protectedBase64);
            var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch { return ""; }
    }

    private static AppSettings NewDefaults()
    {
        var dir = GetAppDir();
        return new AppSettings
        {
            DataDir = dir,
            DbPath = Path.Combine(dir, "finance.sqlite")
        };
    }
}

public sealed class Transaction
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Source { get; set; } = "import";
    public string Account { get; set; } = "";
    public DateTime DateUtc { get; set; }
    public string Description { get; set; } = "";
    public string Merchant { get; set; } = "";
    public decimal Amount { get; set; } // + income, - expense
    public string Currency { get; set; } = "UAH";
    public MoneyCategory Category { get; set; } = MoneyCategory.Unsorted;
    public string Hash { get; set; } = "";
}

public static class HashUtil
{
    public static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

public sealed class Db
{
    private readonly AppSettings _settings;
    public Db(AppSettings settings) => _settings = settings;

    private SqliteConnection Open() => new($"Data Source={_settings.DbPath}");

    public async Task InitAsync()
    {
        await using var con = Open();
        await con.OpenAsync();

        var cmd = con.CreateCommand();
        cmd.CommandText = @"
PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS transactions (
  id TEXT PRIMARY KEY,
  source TEXT NOT NULL,
  account TEXT NOT NULL,
  dateUtc TEXT NOT NULL,
  description TEXT NOT NULL,
  merchant TEXT NOT NULL,
  amount REAL NOT NULL,
  currency TEXT NOT NULL,
  category INTEGER NOT NULL,
  hash TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS merchant_category (
  merchantNorm TEXT PRIMARY KEY,
  category INTEGER NOT NULL,
  updatedUtc TEXT NOT NULL
);
";
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<int> UpsertTransactionsAsync(IEnumerable<Transaction> txs)
    {
        var inserted = 0;
        await using var con = Open();
        await con.OpenAsync();
        await using var tr = await con.BeginTransactionAsync();

        foreach (var t in txs)
        {
            var cmd = con.CreateCommand();
            cmd.Transaction = tr;
            cmd.CommandText = @"
INSERT OR IGNORE INTO transactions
(id, source, account, dateUtc, description, merchant, amount, currency, category, hash)
VALUES ($id, $source, $account, $dateUtc, $description, $merchant, $amount, $currency, $category, $hash);
";
            cmd.Parameters.AddWithValue("$id", t.Id);
            cmd.Parameters.AddWithValue("$source", t.Source);
            cmd.Parameters.AddWithValue("$account", t.Account);
            cmd.Parameters.AddWithValue("$dateUtc", t.DateUtc.ToString("o"));
            cmd.Parameters.AddWithValue("$description", t.Description);
            cmd.Parameters.AddWithValue("$merchant", t.Merchant);
            cmd.Parameters.AddWithValue("$amount", t.Amount);
            cmd.Parameters.AddWithValue("$currency", t.Currency);
            cmd.Parameters.AddWithValue("$category", (int)t.Category);
            cmd.Parameters.AddWithValue("$hash", t.Hash);

            inserted += await cmd.ExecuteNonQueryAsync();
        }

        await tr.CommitAsync();
        return inserted;
    }

    public async Task<List<Transaction>> GetTransactionsAsync(DateTime fromUtc, DateTime toUtc)
    {
        var list = new List<Transaction>();
        await using var con = Open();
        await con.OpenAsync();

        var cmd = con.CreateCommand();
        cmd.CommandText = @"
SELECT id, source, account, dateUtc, description, merchant, amount, currency, category, hash
FROM transactions
WHERE dateUtc >= $from AND dateUtc <= $to
ORDER BY dateUtc DESC;
";
        cmd.Parameters.AddWithValue("$from", fromUtc.ToString("o"));
        cmd.Parameters.AddWithValue("$to", toUtc.ToString("o"));

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new Transaction
            {
                Id = r.GetString(0),
                Source = r.GetString(1),
                Account = r.GetString(2),
                DateUtc = DateTime.Parse(r.GetString(3), null, DateTimeStyles.RoundtripKind),
                Description = r.GetString(4),
                Merchant = r.GetString(5),
                Amount = r.GetDecimal(6),
                Currency = r.GetString(7),
                Category = (MoneyCategory)r.GetInt32(8),
                Hash = r.GetString(9),
            });
        }
        return list;
    }

    public async Task SaveMerchantCategoryAsync(string merchantNorm, MoneyCategory category)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = @"
INSERT INTO merchant_category(merchantNorm, category, updatedUtc)
VALUES($m, $c, $u)
ON CONFLICT(merchantNorm) DO UPDATE SET
  category = excluded.category,
  updatedUtc = excluded.updatedUtc;
";
        cmd.Parameters.AddWithValue("$m", merchantNorm);
        cmd.Parameters.AddWithValue("$c", (int)category);
        cmd.Parameters.AddWithValue("$u", DateTime.UtcNow.ToString("o"));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<MoneyCategory?> TryGetMerchantCategoryAsync(string merchantNorm)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT category FROM merchant_category WHERE merchantNorm = $m LIMIT 1;";
        cmd.Parameters.AddWithValue("$m", merchantNorm);
        var obj = await cmd.ExecuteScalarAsync();
        if (obj is null || obj is DBNull) return null;
        return (MoneyCategory)Convert.ToInt32(obj);
    }

    public async Task UpdateTransactionCategoryAsync(string txId, MoneyCategory cat)
    {
        await using var con = Open();
        await con.OpenAsync();
        var cmd = con.CreateCommand();
        cmd.CommandText = "UPDATE transactions SET category = $c WHERE id = $id;";
        cmd.Parameters.AddWithValue("$c", (int)cat);
        cmd.Parameters.AddWithValue("$id", txId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public sealed class CategorizationService
{
    private readonly Db _db;

    private readonly (Regex rx, MoneyCategory cat)[] _rules = new[]
    {
        (new Regex(@"salary|зарп|аванс|avans", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Income),
        (new Regex(@"коміс|fee|commission", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Fees),
        (new Regex(@"tax|подат", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Taxes),

        (new Regex(@"silpo|сільпо|атб|novus|auchan|metro|market|супермар", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Groceries),
        (new Regex(@"coffee|кафе|restaurant|піца|kfc|mcdonald|кав", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Cafes),

        (new Regex(@"uber|bolt|taxi|metro|bus|tram|train|укрзаліз|transport", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Transport),
        (new Regex(@"wog|okko|shell|fuel|азс|benz", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Fuel),

        (new Regex(@"apteka|pharm|doctor|clinic|стомат|health", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Health),
        (new Regex(@"netflix|spotify|youtube|google one|apple.com/bill|subscription|підписк", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Subscriptions),

        (new Regex(@"rozetka|aliexpress|prom.ua|epicentr|ikea|shopping|магазин", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Shopping),
        (new Regex(@"cinema|movie|game|steam|psn|xbox|entertain", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Entertainment),

        (new Regex(@"utility|комун|kyivstar|lifecell|vodafone|internet", RegexOptions.IgnoreCase|RegexOptions.Compiled), MoneyCategory.Utilities),
    };

    public CategorizationService(Db db) => _db = db;

    public string Normalize(string s) => (s ?? "").Trim().ToLowerInvariant();

    public async Task<MoneyCategory> GuessAsync(Transaction t)
    {
        var key = Normalize(!string.IsNullOrWhiteSpace(t.Merchant) ? t.Merchant : t.Description);
        var learned = await _db.TryGetMerchantCategoryAsync(key);
        if (learned is not null) return learned.Value;

        if (t.Amount > 0) return MoneyCategory.Income;

        var text = $"{t.Merchant} {t.Description}".Trim();
        foreach (var (rx, cat) in _rules)
            if (rx.IsMatch(text)) return cat;

        return MoneyCategory.Unsorted;
    }
}

public sealed class ImportService
{
    private readonly CategorizationService _categorizer;
    public ImportService(CategorizationService categorizer) => _categorizer = categorizer;

    public async Task<List<Transaction>> ImportAsync(string path, string sourceLabel = "import")
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext is ".csv" or ".txt") return await ImportCsvAsync(path, sourceLabel);
        if (ext is ".xlsx") return await ImportXlsxAsync(path, sourceLabel);
        throw new InvalidOperationException("Підтримуються лише CSV або XLSX.");
    }

    private async Task<List<Transaction>> ImportCsvAsync(string path, string sourceLabel)
    {
        await using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs, Encoding.UTF8, true);

        using var csv = new CsvReader(sr, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord?.Select(h => h.Trim()).ToArray() ?? Array.Empty<string>();

        string Find(params string[] variants)
        {
            foreach (var v in variants)
            {
                var h = headers.FirstOrDefault(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase));
                if (h is not null) return h;
            }
            return "";
        }

        var hDate = Find("Date", "Дата", "Дата операції", "Дата операції (Київ)");
        var hAmount = Find("Amount", "Сума", "Сума операції", "Сума в валюті рахунку");
        var hDesc = Find("Description", "Опис", "Призначення", "Деталі", "Comment");
        var hMerchant = Find("Merchant", "Контрагент", "Одержувач", "Назва точки", "Назва торговця");
        var hCurrency = Find("Currency", "Валюта");
        var hAccount = Find("Account", "Рахунок", "IBAN");

        var list = new List<Transaction>();
        while (csv.Read())
        {
            var dateStr = hDate.Length > 0 ? (csv.GetField(hDate) ?? "") : "";
            var amountStr = hAmount.Length > 0 ? (csv.GetField(hAmount) ?? "") : "";
            var desc = hDesc.Length > 0 ? (csv.GetField(hDesc) ?? "") : "";
            var merch = hMerchant.Length > 0 ? (csv.GetField(hMerchant) ?? "") : "";
            var ccy = hCurrency.Length > 0 ? (csv.GetField(hCurrency) ?? "") : "UAH";
            var acc = hAccount.Length > 0 ? (csv.GetField(hAccount) ?? "") : "";

            if (!TryParseDate(dateStr, out var dtLocal)) continue;
            if (!TryParseDecimal(amountStr, out var amount)) continue;

            var t = new Transaction
            {
                Source = sourceLabel,
                Account = acc,
                DateUtc = DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime(),
                Description = desc,
                Merchant = merch,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(ccy) ? "UAH" : ccy.Trim()
            };

            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await _categorizer.GuessAsync(t);
            list.Add(t);
        }
        return list;
    }

    private async Task<List<Transaction>> ImportXlsxAsync(string path, string sourceLabel)
    {
        using var wb = new ClosedXML.Excel.XLWorkbook(path);
        var ws = wb.Worksheets.First();

        var headerRow = 1;
        var maxScan = Math.Min(20, ws.LastRowUsed().RowNumber());
        for (int r = 1; r <= maxScan; r++)
        {
            var rowText = string.Join(" ", ws.Row(r).Cells(1, 25).Select(c => (c.GetString() ?? "").Trim().ToLowerInvariant()));
            if (rowText.Contains("дата") && (rowText.Contains("сума") || rowText.Contains("amount")))
            {
                headerRow = r;
                break;
            }
        }

        var headerCells = ws.Row(headerRow).CellsUsed().ToList();
        var headers = headerCells.Select(c => c.GetString().Trim()).ToList();

        int Col(params string[] variants)
        {
            for (int i = 0; i < headers.Count; i++)
                foreach (var v in variants)
                    if (string.Equals(headers[i], v, StringComparison.OrdinalIgnoreCase))
                        return headerCells[i].Address.ColumnNumber;
            return -1;
        }

        var cDate = Col("Date", "Дата", "Дата операції");
        var cAmount = Col("Amount", "Сума", "Сума операції", "Сума в валюті рахунку");
        var cDesc = Col("Description", "Опис", "Призначення", "Деталі");
        var cMerchant = Col("Merchant", "Контрагент", "Одержувач", "Назва точки", "Назва торговця");
        var cCurrency = Col("Currency", "Валюта");
        var cAccount = Col("Account", "Рахунок", "IBAN");

        var list = new List<Transaction>();
        var lastRow = ws.LastRowUsed().RowNumber();
        for (int r = headerRow + 1; r <= lastRow; r++)
        {
            var dateStr = cDate > 0 ? ws.Cell(r, cDate).GetValue<string>() : "";
            var amountStr = cAmount > 0 ? ws.Cell(r, cAmount).GetValue<string>() : "";
            var desc = cDesc > 0 ? ws.Cell(r, cDesc).GetValue<string>() : "";
            var merch = cMerchant > 0 ? ws.Cell(r, cMerchant).GetValue<string>() : "";
            var ccy = cCurrency > 0 ? ws.Cell(r, cCurrency).GetValue<string>() : "UAH";
            var acc = cAccount > 0 ? ws.Cell(r, cAccount).GetValue<string>() : "";

            if (!TryParseDate(dateStr, out var dtLocal)) continue;
            if (!TryParseDecimal(amountStr, out var amount)) continue;

            var t = new Transaction
            {
                Source = sourceLabel,
                Account = acc,
                DateUtc = DateTime.SpecifyKind(dtLocal, DateTimeKind.Local).ToUniversalTime(),
                Description = desc,
                Merchant = merch,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(ccy) ? "UAH" : ccy.Trim()
            };

            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await _categorizer.GuessAsync(t);
            list.Add(t);
        }

        return list;
    }

    private static bool TryParseDecimal(string s, out decimal v)
    {
        s = (s ?? "").Trim().Replace(" ", "").Replace("\u00A0", "");
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("uk-UA"), out v)
            || decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }

    private static bool TryParseDate(string s, out DateTime dt)
    {
        s = (s ?? "").Trim();
        var cultures = new[] { CultureInfo.GetCultureInfo("uk-UA"), CultureInfo.InvariantCulture };
        foreach (var c in cultures)
        {
            if (DateTime.TryParse(s, c, DateTimeStyles.AssumeLocal, out dt)) return true;
        }
        dt = default;
        return false;
    }
}

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

public sealed class MonobankClient
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri("https://api.monobank.ua") };

    private sealed class ClientInfo { public List<Account> accounts { get; set; } = new(); }
    private sealed class Account { public string id { get; set; } = ""; public string iban { get; set; } = ""; }

    private sealed class Statement
    {
        public long time { get; set; }
        public string description { get; set; } = "";
        public long amount { get; set; }
        public string comment { get; set; } = "";
        public string counterName { get; set; } = "";
    }

    public async Task<List<(string accountId, string label)>> GetAccountsAsync(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "/personal/client-info");
        req.Headers.Add("X-Token", token);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var info = await resp.Content.ReadFromJsonAsync<ClientInfo>();
        return info?.accounts?.Where(a => !string.IsNullOrWhiteSpace(a.id))
            .Select(a => (a.id, string.IsNullOrWhiteSpace(a.iban) ? a.id : a.iban))
            .ToList() ?? new();
    }

    public async Task<List<Transaction>> GetStatementsAsync(
        string token, string accountId, string accountLabel, DateTime fromUtc, DateTime toUtc, CategorizationService categorizer)
    {
        var from = new DateTimeOffset(fromUtc).ToUnixTimeSeconds();
        var to = new DateTimeOffset(toUtc).ToUnixTimeSeconds();

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/personal/statement/{accountId}/{from}/{to}");
        req.Headers.Add("X-Token", token);

        var resp = await _http.SendAsync(req);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadAsStringAsync();
        var items = JsonSerializer.Deserialize<List<Statement>>(json) ?? new();

        var txs = new List<Transaction>();
        foreach (var s in items)
        {
            var amount = s.amount / 100m;
            var dt = DateTimeOffset.FromUnixTimeSeconds(s.time).UtcDateTime;

            var merchant = string.IsNullOrWhiteSpace(s.counterName) ? s.description : s.counterName;
            var desc = string.IsNullOrWhiteSpace(s.comment) ? s.description : $"{s.description} -  {s.comment}";

            var t = new Transaction
            {
                Source = "monobank",
                Account = accountLabel,
                DateUtc = dt,
                Merchant = merchant ?? "",
                Description = desc ?? "",
                Amount = amount,
                Currency = "UAH",
            };

            t.Hash = HashUtil.Sha256($"{t.Source}|{t.Account}|{t.DateUtc:o}|{t.Amount}|{t.Currency}|{t.Merchant}|{t.Description}");
            t.Category = await categorizer.GuessAsync(t);
            txs.Add(t);
        }

        return txs;
    }
}
