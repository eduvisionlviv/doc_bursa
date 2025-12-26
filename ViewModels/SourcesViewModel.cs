using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.ViewModels
{
    public partial class SourcesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly MonobankService _monobank;
        private readonly PrivatBankService _privatbank;
        private readonly UkrsibBankService _ukrsibbank;
        private readonly FileImportService _fileImport;
        private readonly CategorizationService _categorization;

        [ObservableProperty]
        private ObservableCollection<DataSource> dataSources = new();

        [ObservableProperty]
        private DataSource? selectedSource;

        [ObservableProperty]
        private string monobankToken = string.Empty;

        [ObservableProperty]
        private string privatbankClientId = string.Empty;

        [ObservableProperty]
        private string privatbankSecret = string.Empty;

        [ObservableProperty]
        private string ukrsibbankToken = string.Empty;

        [ObservableProperty]
        private bool isSyncing;

        public SourcesViewModel()
        {
            _db = new DatabaseService();
            _monobank = new MonobankService();
            _privatbank = new PrivatBankService();
            _ukrsibbank = new UkrsibBankService();
            _fileImport = new FileImportService();
            _categorization = new CategorizationService(_db);

            LoadSources();
        }

        private void LoadSources()
        {
            DataSources = new ObservableCollection<DataSource>(_db.GetDataSources());

            if (!DataSources.Any(s => s.Name == "Monobank"))
                DataSources.Add(new DataSource { Name = "Monobank", Type = "API" });
            if (!DataSources.Any(s => s.Name == "PrivatBank"))
                DataSources.Add(new DataSource { Name = "PrivatBank", Type = "API" });
            if (!DataSources.Any(s => s.Name == "Ukrsibbank"))
                DataSources.Add(new DataSource { Name = "Ukrsibbank", Type = "API" });
        }

        [RelayCommand]
        private async Task SyncMonobankAsync()
        {
            if (string.IsNullOrEmpty(MonobankToken))
            {
                MessageBox.Show("Введіть токен Monobank", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSyncing = true;
            try
            {
                var from = DateTime.Now.AddMonths(-1);
                var to = DateTime.Now;
                var transactions = await _monobank.FetchTransactionsAsync(MonobankToken, from, to);

                foreach (var t in transactions)
                {
                    t.Category = _categorization.CategorizeTransaction(t);
                    _db.SaveTransaction(t);
                }

                var source = DataSources.FirstOrDefault(s => s.Name == "Monobank");
                if (source != null)
                {
                    source.ApiToken = MonobankToken;
                    source.IsEnabled = true;
                    source.LastSync = DateTime.Now;
                    _db.SaveDataSource(source);
                }

                MessageBox.Show($"Синхронізовано {transactions.Count} транзакцій", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        [RelayCommand]
        private async Task SyncPrivatBankAsync()
        {
            if (string.IsNullOrEmpty(PrivatbankClientId) || string.IsNullOrEmpty(PrivatbankSecret))
            {
                MessageBox.Show("Введіть дані авторизації PrivatBank або імпортуйте файл", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSyncing = true;
            try
            {
                var from = DateTime.Now.AddMonths(-1);
                var to = DateTime.Now;
                var transactions = await _privatbank.FetchTransactionsAsync(PrivatbankClientId, PrivatbankSecret, from, to);

                if (!transactions.Any())
                {
                    MessageBox.Show("API не вдалося отримати дані. Використайте імпорт файлу", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var t in transactions)
                {
                    t.Category = _categorization.CategorizeTransaction(t);
                    _db.SaveTransaction(t);
                }

                var source = DataSources.FirstOrDefault(s => s.Name == "PrivatBank");
                if (source != null)
                {
                    source.ClientId = PrivatbankClientId;
                    source.ClientSecret = PrivatbankSecret;
                    source.IsEnabled = true;
                    source.LastSync = DateTime.Now;
                    _db.SaveDataSource(source);
                }

                MessageBox.Show($"Синхронізовано {transactions.Count} транзакцій", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка API: {ex.Message}. Використайте імпорт файлу", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        [RelayCommand]
        private async Task SyncUkrsibbankAsync()
        {
            if (string.IsNullOrEmpty(UkrsibbankToken))
            {
                MessageBox.Show("Введіть токен Ukrsibbank або імпортуйте файл", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsSyncing = true;
            try
            {
                var from = DateTime.Now.AddMonths(-1);
                var to = DateTime.Now;
                var transactions = await _ukrsibbank.FetchTransactionsAsync(UkrsibbankToken, from, to);

                if (!transactions.Any())
                {
                    MessageBox.Show("API не вдалося отримати дані. Використайте імпорт файлу", "Увага", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                foreach (var t in transactions)
                {
                    t.Category = _categorization.CategorizeTransaction(t);
                    _db.SaveTransaction(t);
                }

                var source = DataSources.FirstOrDefault(s => s.Name == "Ukrsibbank");
                if (source != null)
                {
                    source.ApiToken = UkrsibbankToken;
                    source.IsEnabled = true;
                    source.LastSync = DateTime.Now;
                    _db.SaveDataSource(source);
                }

                MessageBox.Show($"Синхронізовано {transactions.Count} транзакцій", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка API: {ex.Message}. Використайте імпорт файлу", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsSyncing = false;
            }
        }

        [RelayCommand]
        private void ImportFile(string bankName)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV файли (*.csv)|*.csv|Excel файли (*.xlsx)|*.xlsx",
                Title = $"Імпорт виписки {bankName}"
            };

            if (dialog.ShowDialog() == true)
            {
                var transactions = _fileImport.ImportFile(dialog.FileName, bankName);

                foreach (var t in transactions)
                {
                    t.Category = _categorization.CategorizeTransaction(t);
                    _db.SaveTransaction(t);
                }

                MessageBox.Show($"Імпортовано {transactions.Count} транзакцій", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
