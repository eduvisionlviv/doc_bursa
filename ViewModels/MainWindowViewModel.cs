using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk;

public sealed partial class TransactionRow : ObservableObject
{
    public string Id { get; init; } = "";
    public string Source { get; init; } = "";
    public string Description { get; init; } = "";
    public string Merchant { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime DateUtc { get; init; }

    public string DateLocal => DateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    [ObservableProperty] private MoneyCategory category;
}

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly Db _db;
    private readonly CategorizationService _categorizer;
    private readonly AnalyticsService _analytics;
    private readonly MonobankClient _mono;

    public ObservableCollection<PeriodPreset> PeriodPresets { get; } = new()
    {
        new PeriodPreset("this_month", "Поточний місяць"),
        new PeriodPreset("last_month", "Минулий місяць"),
        new PeriodPreset("this_year", "Поточний рік"),
        new PeriodPreset("custom", "Довільний період")
    };

    [ObservableProperty] private PeriodPreset selectedPreset;
    [ObservableProperty] private DateTimeOffset? fromDate;
    [ObservableProperty] private DateTimeOffset? toDate;

    [ObservableProperty] private string statusText = "Готово";
    [ObservableProperty] private string infoText = "Готово";

    [ObservableProperty] private string monobankToken = "";

    public ObservableCollection<string> ImportLog { get; } = new();
    public ObservableCollection<TransactionRow> Transactions { get; } = new();
    public ObservableCollection<MoneyCategory> Categories { get; } = new(Enum.GetValues<MoneyCategory>());

    [ObservableProperty] private decimal income;
    [ObservableProperty] private decimal expense;
    [ObservableProperty] private decimal net;

    [ObservableProperty] private ISeries[] pieSeries = Array.Empty<ISeries>();
    [ObservableProperty] private ISeries[] lineSeries = Array.Empty<ISeries>();
    [ObservableProperty] private Axis[] xAxes = Array.Empty<Axis>();
    [ObservableProperty] private Axis[] yAxes = Array.Empty<Axis>();

    public MainWindowViewModel(AppSettings settings, Db db, CategorizationService categorizer, AnalyticsService analytics, MonobankClient mono)
    {
        _settings = settings;
        _db = db;
        _categorizer = categorizer;
        _analytics = analytics;
        _mono = mono;

        MonobankToken = SettingsService.Unprotect(_settings.MonoTokenProtected);

        SelectedPreset = PeriodPresets;
        ApplyPreset(SelectedPreset);
    }

    partial void OnSelectedPresetChanged(PeriodPreset value) => ApplyPreset(value);

    private void ApplyPreset(PeriodPreset p)
    {
        var now = DateTime.Now;
        DateTime from, to;

        if (p.Key == "this_month")
        {
            from = new DateTime(now.Year, now.Month, 1);
            to = from.AddMonths(1).AddSeconds(-1);
        }
        else if (p.Key == "last_month")
        {
            var m = new DateTime(now.Year, now.Month, 1).AddMonths(-1);
            from = m;
            to = m.AddMonths(1).AddSeconds(-1);
        }
        else if (p.Key == "this_year")
        {
            from = new DateTime(now.Year, 1, 1);
            to = new DateTime(now.Year, 12, 31, 23, 59, 59);
        }
        else
        {
            FromDate ??= DateTimeOffset.Now.AddDays(-30);
            ToDate ??= DateTimeOffset.Now;
            return;
        }

        FromDate = new DateTimeOffset(from);
        ToDate = new DateTimeOffset(to);
    }

    private (DateTime fromUtc, DateTime toUtc) PeriodUtc()
    {
        var from = (FromDate ?? DateTimeOffset.Now.AddDays(-30)).DateTime;
        var to = (ToDate ?? DateTimeOffset.Now).DateTime;
        return (DateTime.SpecifyKind(from, DateTimeKind.Local).ToUniversalTime(),
                DateTime.SpecifyKind(to, DateTimeKind.Local).ToUniversalTime());
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        _settings.MonoTokenProtected = SettingsService.Protect(MonobankToken);
        await SettingsService.SaveAsync(_settings);
        InfoText = "Налаштування збережено.";
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        StatusText = "Оновлення…";
        var (fromUtc, toUtc) = PeriodUtc();

        await ReloadTransactionsAsync(fromUtc, toUtc);
        await ReloadDashboardAsync(fromUtc, toUtc);

        StatusText = "Готово";
    }

    private async Task ReloadTransactionsAsync(DateTime fromUtc, DateTime toUtc)
    {
        Transactions.Clear();
        var txs = await _db.GetTransactionsAsync(fromUtc, toUtc);
        foreach (var t in txs)
        {
            Transactions.Add(new TransactionRow
            {
                Id = t.Id,
                Source = t.Source,
                Description = t.Description,
                Merchant = t.Merchant,
                Amount = t.Amount,
                DateUtc = t.DateUtc,
                Category = t.Category
            });
        }
    }

    private async Task ReloadDashboardAsync(DateTime fromUtc, DateTime toUtc)
    {
        var cards = await _analytics.GetCardsAsync(fromUtc, toUtc);
        Income = cards.income;
        Expense = cards.expense;
        Net = cards.net;

        var byCat = await _analytics.ByCategoryAsync(fromUtc, toUtc);
        PieSeries = byCat.Where(kv => kv.Value > 0)
                         .OrderByDescending(kv => kv.Value)
                         .Take(8)
                         .Select(kv => (ISeries)new PieSeries<decimal> { Values = new[] { kv.Value }, Name = kv.Key.ToString() })
                         .ToArray();

        var byDay = await _analytics.ExpenseByDayAsync(fromUtc, toUtc);
        var xs = byDay.Keys.Select(d => d.ToString("MM-dd")).ToArray();
        var ys = byDay.Values.ToArray();

        LineSeries = new ISeries[]
        {
            new LineSeries<decimal> { Values = ys, Name = "Витрати/день", GeometrySize = 6 }
        };
        XAxes = new[] { new Axis { Labels = xs } };
        YAxes = new[] { new Axis { } };
    }

    public async Task ImportFilesAsync(string[] paths)
    {
        StatusText = "Імпорт…";
        var importer = new ImportService(_categorizer);

        var totalNew = 0;
        foreach (var p in paths)
        {
            var txs = await importer.ImportAsync(p, "import");
            totalNew += await _db.UpsertTransactionsAsync(txs);
            ImportLog.Add($"{System.IO.Path.GetFileName(p)} → +{txs.Count} (нових: {totalNew})");
        }

        await RefreshAsync();
    }

    public async Task SetCategoryAsync(TransactionRow row, MoneyCategory cat)
    {
        if (row.Category == cat) return;

        row.Category = cat;
        await _db.UpdateTransactionCategoryAsync(row.Id, cat);

        // learning (merchant->category)
        var key = _categorizer.Normalize(row.Merchant.Length > 0 ? row.Merchant : row.Description);
        if (!string.IsNullOrWhiteSpace(key))
            await _db.SaveMerchantCategoryAsync(key, cat);

        await RefreshAsync();
    }

    [RelayCommand]
    public async Task SyncMonobankAsync()
    {
        if (string.IsNullOrWhiteSpace(MonobankToken))
        {
            InfoText = "Введи X-Token для Monobank.";
            return;
        }

        await SaveSettingsAsync();

        try
        {
            StatusText = "Monobank: акаунти…";
            var accounts = await _mono.GetAccountsAsync(MonobankToken);

            if (accounts.Count == 0)
            {
                InfoText = "Monobank: не знайдено рахунків (перевір токен).";
                return;
            }

            var (fromUtc, toUtc) = PeriodUtc();
            var inserted = 0;

            foreach (var (accountId, label) in accounts.Take(3))
            {
                StatusText = $"Monobank: виписка ({label})…";
                var txs = await _mono.GetStatementsAsync(MonobankToken, accountId, label, fromUtc, toUtc, _categorizer);
                inserted += await _db.UpsertTransactionsAsync(txs);
            }

            InfoText = $"Monobank: синхронізація завершена, додано {inserted} нових.";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            InfoText = "Monobank: помилка синхронізації. Спробуй імпорт файлів. " + ex.Message;
        }
        finally
        {
            StatusText = "Готово";
        }
    }
}
