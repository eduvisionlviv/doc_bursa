using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;
using Microsoft.Win32;
using Serilog;

namespace doc_bursa.ViewModels
{
    public partial class SourcesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly TransactionService _transactionService;
        private readonly CsvImportService _csvImport;
        private readonly ExcelImportService _excelImport;
        private readonly ImportLogService _importLog;
        private readonly SyncQueueService _syncQueue;
        private readonly ILogger _logger;

        [ObservableProperty]
        private ObservableCollection<DataSource> sources = new();

        [ObservableProperty]
        private DataSource? selectedSource; // Для редагування

        [ObservableProperty]
        private bool isAddingSource;

        [ObservableProperty]
        private string formTitle = "Нове джерело"; // Динамічний заголовок

        [ObservableProperty]
        private string newSourceName = string.Empty;

        [ObservableProperty]
        private string newSourceType = "PrivatBank";

        [ObservableProperty]
        private string newSourceToken = string.Empty;

        [ObservableProperty]
        private string newSourceClientId = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        [ObservableProperty]
        private bool isOffline;

        [ObservableProperty]
        private string networkStatus = "Статус мережі: невідомо";

        [ObservableProperty]
        private string syncStatusMessage = "Готово";

        public string[] AvailableTypes { get; } = { "PrivatBank", "Monobank", "Ukrsibbank", "CSV Import" };

        public SourcesViewModel()
        {
            _db = new DatabaseService();
            _logger = Log.ForContext<SourcesViewModel>();
            
            var catService = new CategorizationService(_db);
            var dedupService = new DeduplicationService(_db);
            _transactionService = new TransactionService(_db, dedupService, catService);
            _csvImport = new CsvImportService(_db, catService, _transactionService);
            _excelImport = new ExcelImportService(_db, catService, _transactionService);
            _importLog = new ImportLogService();
            _syncQueue = new SyncQueueService();
            _syncQueue.StatusChanged += message => SyncStatusMessage = message;
            _syncQueue.NetworkStatusChanged += (online, message) =>
            {
                IsOffline = !online;
                NetworkStatus = $"Статус мережі: {message}";
                _logger.Information("Network status changed: {Message}", message);
            };

            _ = LoadSources();
            _ = LoadGroups();
        }

        [RelayCommand]
        private async Task LoadSources()
        {
            try 
            {
                var items = await _db.GetDataSourcesAsync();
                Sources = new ObservableCollection<DataSource>(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка завантаження: {ex.Message}", "Помилка");
            }
        }

        [RelayCommand]
        private async Task LoadGroups()
        {
            try
            {
                var groups = await _db.GetMasterGroupsAsync();
                AccountGroups = new ObservableCollection<MasterGroup>(groups);
                SelectedImportGroup ??= AccountGroups.FirstOrDefault();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити групи: {ex.Message}", "Помилка");
            }
        }

        // --- ДОДАВАННЯ ---
        [RelayCommand]
        private void StartAddSource()
        {
            SelectedSource = null; // Скидаємо вибір (режим створення)
            FormTitle = "Нове джерело";
            
            NewSourceName = "";
            NewSourceType = "PrivatBank";
            NewSourceToken = "";
            NewSourceClientId = "";
            
            IsAddingSource = true;
        }

        // --- РЕДАГУВАННЯ (НОВЕ) ---
        [RelayCommand]
        private void StartEditSource(DataSource source)
        {
            if (source == null) return;

            SelectedSource = source; // Запам'ятовуємо джерело
            FormTitle = "Редагування джерела";

            // Заповнюємо форму даними
            NewSourceName = source.Name;
            NewSourceType = source.Type;
            NewSourceToken = source.ApiToken ?? "";
            NewSourceClientId = source.ClientId ?? "";

            IsAddingSource = true; // Відкриваємо форму
        }

        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingSource = false;
            SelectedSource = null;
        }

        // --- ЗБЕРЕЖЕННЯ (Оновлено) ---
        [RelayCommand]
        private async Task SaveSourceAsync()
        {
            if (string.IsNullOrWhiteSpace(NewSourceName))
            {
                MessageBox.Show("Введіть назву джерела!", "Помилка");
                return;
            }

            if (NewSourceType != "CSV Import" && string.IsNullOrWhiteSpace(NewSourceToken))
            {
                MessageBox.Show("Для API потрібен токен!", "Помилка");
                return;
            }

            IsBusy = true;

            try 
            {
                if (SelectedSource != null)
                {
                    // === РЕДАГУВАННЯ ===
                    SelectedSource.Name = NewSourceName;
                    SelectedSource.Type = NewSourceType;
                    SelectedSource.ApiToken = NewSourceToken;
                    SelectedSource.ClientId = NewSourceClientId;

                    await _db.UpdateDataSourceAsync(SelectedSource);
                    MessageBox.Show("Зміни збережено!", "Успіх");
                }
                else
                {
                    // === СТВОРЕННЯ НОВОГО ===
                    var source = new DataSource
                    {
                        Name = NewSourceName,
                        Type = NewSourceType,
                        ApiToken = NewSourceToken,
                        ClientId = NewSourceClientId,
                        IsEnabled = true
                    };
                    await _db.AddDataSourceAsync(source);
                    MessageBox.Show("Джерело додано!", "Успіх");
                }

                await LoadSources();
                IsAddingSource = false;
                SelectedSource = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка збереження: {ex.Message}", "Помилка");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteSource(DataSource source)
        {
            if (MessageBox.Show($"Видалити {source.Name}?", "Увага", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await _db.DeleteDataSourceAsync(source.Id);
                await LoadSources();
            }
        }

        [RelayCommand]
        private async Task ToggleSource(DataSource source)
        {
            if (source == null) return;
            try
            {
                IsBusy = true;
                source.IsEnabled = !source.IsEnabled;
                await _db.UpdateDataSourceAsync(source);
                await LoadSources(); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося змінити статус: {ex.Message}", "Помилка");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task SyncSource(DataSource source)
        {
            try
            {
                IsBusy = true;
                SyncStatusMessage = "Додаємо у чергу синхронізації...";
                var importedCount = 0;

                await _syncQueue.EnqueueAsync($"Синхронізація {source.Name}", async _ =>
                {
                    List<Transaction> transactions = new();
                    var toDate = DateTime.Now;
                    var fromDate = toDate.AddMonths(-1);

                    if (source.Type == "PrivatBank")
                    {
                        var service = new PrivatBankService();
                        transactions = await service.GetTransactionsAsync(source.ApiToken, source.ClientId, fromDate, toDate);
                    }
                    else if (source.Type == "Monobank")
                    {
                        var service = new MonobankService();
                        transactions = await service.GetTransactionsAsync(source.ApiToken, source.ClientId, fromDate, toDate);
                    }
                    else if (source.Type == "Ukrsibbank")
                    {
                        throw new InvalidOperationException("Для УкрСиббанку використовуйте імпорт CSV.");
                    }

                    if (transactions.Any())
                    {
                        await _transactionService.ImportTransactionsAsync(transactions, CancellationToken.None);
                        source.LastSync = DateTime.Now;
                        await _db.UpdateDataSourceAsync(source);
                        importedCount = transactions.Count;
                    }
                });

                await LoadSources();

                if (importedCount > 0)
                {
                    MessageBox.Show($"Успішно завантажено {importedCount} транзакцій!", "Успіх");
                }
                else
                {
                    MessageBox.Show("Нових транзакцій не знайдено.", "Інфо");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Sync failed for {Source}", source?.Name);
                MessageBox.Show($"Помилка синхронізації: {ex.Message}", "Помилка");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task ImportCsv()
        {
            var dialog = new OpenFileDialog { Filter = "CSV файли|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                var groupId = SelectedImportGroup?.Id;
                var virtualAccount = _db.EnsureVirtualAccountForGroup(groupId, SelectedImportGroup?.Name ?? "Manual CSV");
                await _csvImport.ImportFromCsvAsync(dialog.FileName, "universal", null, CancellationToken.None, groupId, virtualAccount);
                IsBusy = false;
                MessageBox.Show("CSV імпортовано.");
            }
        }

        [RelayCommand]
        private async Task ImportExcel()
        {
             var dialog = new OpenFileDialog { Filter = "Excel файли|*.xlsx" };
            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                var groupId = SelectedImportGroup?.Id;
                var virtualAccount = _db.EnsureVirtualAccountForGroup(groupId, SelectedImportGroup?.Name ?? "Manual Excel");
                await _excelImport.ImportFromExcelAsync(dialog.FileName, null, null, CancellationToken.None, groupId, virtualAccount);
                IsBusy = false;
                MessageBox.Show("Excel імпортовано.");
            }
        }

        [RelayCommand]
        private async Task MapAccounts(DataSource source)
        {
            try
            {
                MappingSource = source;
                IsMappingAccounts = true;
                await EnsureDiscoveredAsync(source);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити рахунки: {ex.Message}", "Помилка");
                IsMappingAccounts = false;
            }
        }

        [RelayCommand]
        private async Task SaveMappingsAsync()
        {
            if (MappingSource == null)
            {
                return;
            }

            try
            {
                MappingSource.DiscoveredAccounts = DiscoveredAccounts.ToList();
                await _db.UpdateDataSourceAsync(MappingSource);
                IsMappingAccounts = false;
                await LoadSources();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося зберегти мапінг: {ex.Message}", "Помилка");
            }
        }

        [RelayCommand]
        private void CancelMapping()
        {
            IsMappingAccounts = false;
            MappingSource = null;
            DiscoveredAccounts.Clear();
        }

        private async Task EnsureDiscoveredAsync(DataSource source)
        {
            if (AccountGroups == null || !AccountGroups.Any())
            {
                await LoadGroups();
            }

            List<DiscoveredAccount> accounts = source.DiscoveredAccounts?.Any() == true
                ? source.DiscoveredAccounts
                : new List<DiscoveredAccount>();

            if (!accounts.Any())
            {
                if (source.Type == "Monobank")
                {
                    var service = new MonobankService();
                    accounts = await service.DiscoverAccountsAsync(source.ApiToken);
                    source.PingStatus = "Monobank OK";
                }
                else if (source.Type == "PrivatBank")
                {
                    var service = new PrivatBankService();
                    accounts = await service.DiscoverAccountsAsync(source.ApiToken, source.ClientId);
                    source.PingStatus = "PrivatBank OK";
                }
                else if (source.Type == "Ukrsibbank")
                {
                    var service = new UkrsibBankService();
                    accounts = await service.DiscoverAccountsAsync(source.ApiToken);
                    source.PingStatus = "Ukrsibbank OK";
                }
            }

            DiscoveredAccounts = new ObservableCollection<DiscoveredAccount>(accounts);
            source.DiscoveredAccounts = accounts;
            await _db.UpdateDataSourceAsync(source);
        }
    }
}
