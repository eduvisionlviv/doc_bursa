using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FinDesk.ViewModels;

public sealed partial class SourcesViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly Db _db;
    private readonly CategorizationService _categorizer;
    private readonly MonobankClient _mono;
    private readonly PrivatAutoclientClient _privat;
    private readonly UkrsibClientStub _ukrsib;
    private readonly MainWindowViewModel _shell;

    [ObservableProperty] private string monobankToken = "";
    [ObservableProperty] private string privatToken = "";
    [ObservableProperty] private string privatClientId = "";
    [ObservableProperty] private string privatBaseUrl = "";
    [ObservableProperty] private string ukrsibCertificatePath = "";

    [ObservableProperty] private string infoText = "";

    public SourcesViewModel(
        AppSettings settings,
        Db db,
        CategorizationService categorizer,
        MonobankClient mono,
        PrivatAutoclientClient privat,
        UkrsibClientStub ukrsib,
        MainWindowViewModel shell)
    {
        _settings = settings;
        _db = db;
        _categorizer = categorizer;
        _mono = mono;
        _privat = privat;
        _ukrsib = ukrsib;
        _shell = shell;

        MonobankToken = SettingsService.Unprotect(_settings.MonoTokenProtected);
        PrivatToken = SettingsService.Unprotect(_settings.PrivatTokenProtected);
        PrivatClientId = _settings.PrivatClientId ?? "";
        PrivatBaseUrl = _settings.PrivatBaseUrl ?? "https://acp.privatbank.ua";
        UkrsibCertificatePath = _settings.UkrsibCertificatePath ?? "";

        InfoText = _ukrsib.IsAdvancedModeRequired
            ? "UKRSIB Open Banking у продуктиві зазвичай потребує TPP-статусу та сертифікатів; для користувача рекомендовано імпорт CSV/XLSX."
            : "";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        _settings.MonoTokenProtected = SettingsService.Protect(MonobankToken);
        _settings.PrivatTokenProtected = SettingsService.Protect(PrivatToken);
        _settings.PrivatClientId = PrivatClientId;
        _settings.PrivatBaseUrl = PrivatBaseUrl;
        _settings.UkrsibCertificatePath = UkrsibCertificatePath;

        await SettingsService.SaveAsync(_settings);
        await _shell.SetStatusAsync("Налаштування збережено");
    }

    [RelayCommand]
    private async Task SyncMonobankAsync()
    {
        if (string.IsNullOrWhiteSpace(MonobankToken))
        {
            InfoText = "Введи X-Token для Monobank.";
            return;
        }

        var (fromUtc, toUtc) = _shell.GetPeriodUtc();
        await _shell.SetStatusAsync("Monobank: отримання рахунків…");

        var accounts = await _mono.GetAccountsAsync(MonobankToken);
        if (accounts.Count == 0)
        {
            InfoText = "Monobank: рахунків не знайдено або токен не має доступу.";
            return;
        }

        var totalInserted = 0;
        foreach (var (accountId, iban) in accounts.Take(3)) // keep UI fast; can expand later
        {
            foreach (var (chunkFrom, chunkTo) in _mono.ChunkBy31Days(fromUtc, toUtc))
            {
                await _shell.SetStatusAsync($"Monobank: виписка {chunkFrom:yyyy-MM-dd}…");
                var txs = await _mono.GetStatementsAsync(
                    MonobankToken, accountId, chunkFrom, chunkTo,
                    accountLabel: string.IsNullOrWhiteSpace(iban) ? accountId : iban,
                    categorizer: _categorizer);

                totalInserted += await _db.UpsertTransactionsAsync(txs);
            }
        }

        InfoText = $"Monobank: синхронізація завершена, додано {totalInserted} нових транзакцій.";
        await _shell.RefreshAll();
    }

    [RelayCommand]
    private async Task TestPrivatAsync()
    {
        var ok = await _privat.TestAsync(PrivatBaseUrl, PrivatToken, PrivatClientId);
        InfoText = ok
            ? "Privat API: з’єднання успішне (перевірка базового доступу)."
            : "Privat API: не вдалося підключитись. Рекомендація: імпорт CSV/XLSX виписки.";
    }
}
