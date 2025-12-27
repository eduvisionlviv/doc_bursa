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
        
        // Сервіси імпорту
        private readonly CsvImportService _csvImport;
        private readonly ExcelImportService _excelImport;
        private readonly ImportLogService _importLog;

        [ObservableProperty]
        private ObservableCollection<DataSource> sources = new();

        // Ця властивість потрібна для редагування (зберігає те джерело, яке ми редагуємо)
        [ObservableProperty]
        private DataSource? selectedSource; 

        [ObservableProperty]
        private bool isAddingSource;

        // Заголовок форми: "Нове джерело" або "Редагування"
        [ObservableProperty]
        private string formTitle = "Нове джерело";

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
            
            // Ініціалізація сервісів
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

        // --- ДОДАВАННЯ ---
        [RelayCommand]
        private void StartAddSource()
        {
            SelectedSource = null; // Це режим додавання, а не редагування
            FormTitle = "Нове джерело";

            IsAddingSource = true;
            NewSourceName = "";
            NewSourceType = "PrivatBank";
            NewSourceToken = "";
            NewSourceClientId = "";
        }

        // --- РЕДАГУВАННЯ (НОВЕ) ---
        [RelayCommand]
        private void StartEditSource(DataSource source)
        {
            if (source == null) return;

            SelectedSource = source; // Запам'ятовуємо, що редагуємо
            FormTitle = "Редагування джерела";

            // Заповнюємо поля даними з вибраного джерела
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
            SelectedSource = null; // Скидаємо вибір
        }

        // --- ЛОГІКА ЗБЕРЕЖЕННЯ (Оновлена) ---
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

                    // Оновлюємо в базі
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

                await LoadSources(); // Оновлюємо список на екрані
                IsAddingSource = false; // Закриваємо форму
                SelectedSource = null; // Скидаємо вибір
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

        // --- ПОВЕРНУТИЙ МЕТОД TOGGLE ---
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

        // --- ГОЛОВНА ЛОГІКА СИНХРОНІЗАЦІЇ ---
        [RelayCommand]
        private async Task SyncSource(DataSource source)
        {
            try
            {
                IsBusy = true;
                List<Transaction> transactions = new();
                
                var toDate = DateTime.Now;
                var fromDate = toDate.AddMonths(-1); // Останній місяць

                if (source.Type == "PrivatBank")
                {
                    var service = new PrivatBankService();
                    transactions = await service.GetTransactionsAsync(source.ApiToken, source.ClientId, fromDate, toDate);
                }
                else if (source.Type == "Monobank")
                {
                    // 👇 ПІДКЛЮЧИВ СЕРВІС МОНОБАНКУ
                    var service = new MonobankService();
                    transactions = await service.GetTransactionsAsync(source.ApiToken, source.ClientId, fromDate, toDate);
                }
                else if (source.Type == "Ukrsibbank")
                {
                     MessageBox.Show("Для УкрСиббанку використовуйте імпорт файлів CSV (кнопка зверху).", "Інфо");
                     return;
                }

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
                MessageBox.Show($"Помилка синхронізації:\n{ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
