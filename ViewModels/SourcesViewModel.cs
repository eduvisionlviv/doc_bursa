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

namespace doc_bursa.ViewModels
{
    public partial class SourcesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly TransactionService _transactionService;
        
        // Сервіси імпорту файлів (залишаємо як було)
        private readonly CsvImportService _csvImport;
        private readonly ExcelImportService _excelImport;
        private readonly ImportLogService _importLog;

        [ObservableProperty]
        private ObservableCollection<DataSource> sources = new();

        [ObservableProperty]
        private bool isAddingSource;

        [ObservableProperty]
        private string newSourceName = string.Empty;

        [ObservableProperty]
        private string newSourceType = "PrivatBank"; // За замовчуванням Приват

        [ObservableProperty]
        private string newSourceToken = string.Empty;

        [ObservableProperty]
        private string newSourceClientId = string.Empty;

        [ObservableProperty]
        private bool isBusy;

        public string[] AvailableTypes { get; } = { "PrivatBank", "Monobank", "Ukrsibbank", "CSV Import" };

        public SourcesViewModel()
        {
            _db = new DatabaseService();
            
            // Ініціалізація допоміжних сервісів
            var catService = new CategorizationService(_db);
            var dedupService = new DeduplicationService(_db);
            _transactionService = new TransactionService(_db, dedupService);
            _csvImport = new CsvImportService(_db, catService, _transactionService);
            _excelImport = new ExcelImportService(_db, catService, _transactionService);
            _importLog = new ImportLogService();

            _ = LoadSources();
        }

        [RelayCommand]
        private async Task LoadSources()
        {
            var items = await _db.GetDataSourcesAsync();
            Sources = new ObservableCollection<DataSource>(items);
        }

        [RelayCommand]
        private void StartAddSource()
        {
            IsAddingSource = true;
            NewSourceName = "";
            NewSourceToken = "";
            NewSourceClientId = "";
        }

        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingSource = false;
        }

        // --- ЛОГІКА ЗБЕРЕЖЕННЯ ---
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

            var source = new DataSource
            {
                Name = NewSourceName,
                Type = NewSourceType,
                ApiToken = NewSourceToken,
                ClientId = NewSourceClientId, // Тут може бути номер рахунку для Привату
                IsEnabled = true
            };

            try 
            {
                await _db.AddDataSourceAsync(source);
                await LoadSources();
                IsAddingSource = false;
                MessageBox.Show("Джерело збережено!", "Успіх");
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

        // --- ГОЛОВНА ЛОГІКА СИНХРОНІЗАЦІЇ (РОЗДІЛЕНА) ---
        [RelayCommand]
        private async Task SyncSource(DataSource source)
        {
            try
            {
                IsBusy = true;
                List<Transaction> transactions = new();
                
                // Період синхронізації (наприклад, останній місяць)
                var toDate = DateTime.Now;
                var fromDate = toDate.AddMonths(-1);

                // --- ТУТ МИ РОЗДІЛЯЄМО ЛОГІКУ ---
                if (source.Type == "PrivatBank")
                {
                    var service = new PrivatBankService();
                    // Передаємо Токен і ClientId (як номер рахунку, якщо є)
                    transactions = await service.GetTransactionsAsync(source.ApiToken, source.ClientId, fromDate, toDate);
                }
                else if (source.Type == "Monobank")
                {
                    var service = new MonobankService();
                    // MonobankService треба оновити, або використовувати старий, якщо він працює
                    // transactions = await service.GetTransactionsAsync(...)
                    MessageBox.Show("Monobank поки не налаштований у цьому коді.", "Інфо");
                    return; 
                }
                else if (source.Type == "Ukrsibbank")
                {
                     MessageBox.Show("Для УкрСиббанку використовуйте імпорт файлів CSV.", "Інфо");
                     return;
                }

                // Збереження отриманих транзакцій
                if (transactions.Any())
                {
                    await _transactionService.ImportTransactionsAsync(transactions, CancellationToken.None);
                    
                    source.LastSync = DateTime.Now;
                    await _db.UpdateDataSourceAsync(source);
                    await LoadSources();

                    MessageBox.Show($"Успішно завантажено {transactions.Count} транзакцій!", "Успіх");
                }
                else
                {
                    MessageBox.Show("Нових транзакцій не знайдено.", "Інфо");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Критична помилка синхронізації:\n{ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // --- Імпорт файлів (залишаємо як є) ---
        [RelayCommand]
        private async Task ImportCsv()
        {
            var dialog = new OpenFileDialog { Filter = "CSV файли|*.csv" };
            if (dialog.ShowDialog() == true)
            {
                IsBusy = true;
                await _csvImport.ImportFromCsvAsync(dialog.FileName, "universal", null, CancellationToken.None);
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
                await _excelImport.ImportFromExcelAsync(dialog.FileName, null, null, CancellationToken.None);
                IsBusy = false;
                MessageBox.Show("Excel імпортовано.");
            }
        }
    }
}
