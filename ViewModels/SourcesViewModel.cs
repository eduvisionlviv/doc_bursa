using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinDesk.Models;
using FinDesk.Services;
using Microsoft.Win32;

namespace FinDesk.ViewModels
{
    public partial class SourcesViewModel : ObservableObject
    {
        private readonly DatabaseService _db;
        private readonly CsvImportService _csvImport;
        private readonly CategorizationService _categorization;
        private readonly TransactionService _transactionService;

        [ObservableProperty]
        private ObservableCollection<DataSource> sources = new();

        [ObservableProperty]
        private DataSource? selectedSource;

        [ObservableProperty]
        private bool isAddingSource;

        [ObservableProperty]
        private string newSourceName = string.Empty;

        [ObservableProperty]
        private string newSourceType = "Monobank";

        [ObservableProperty]
        private string newSourceToken = string.Empty;

        [ObservableProperty]
        private string newSourceClientId = string.Empty;

        [ObservableProperty]
        private string newSourceClientSecret = string.Empty;

        public string[] AvailableTypes { get; } = { "Monobank", "PrivatBank", "Ukrsibbank", "CSV Import" };

        public SourcesViewModel()
        {
            _db = new DatabaseService();
            _categorization = new CategorizationService(_db);
            var deduplicationService = new DeduplicationService(_db);
            _transactionService = new TransactionService(_db, deduplicationService);
            _csvImport = new CsvImportService(_db, _categorization, _transactionService);
            LoadSources();
        }

        [RelayCommand]
        private void LoadSources()
        {
            Sources = new ObservableCollection<DataSource>(_db.GetDataSources());
        }

        [RelayCommand]
        private void StartAddSource()
        {
            IsAddingSource = true;
            NewSourceName = string.Empty;
            NewSourceType = "Monobank";
            NewSourceToken = string.Empty;
            NewSourceClientId = string.Empty;
            NewSourceClientSecret = string.Empty;
        }

        [RelayCommand]
        private async Task SaveSource()
        {
            if (string.IsNullOrWhiteSpace(NewSourceName))
            {
                MessageBox.Show("Введіть назву джерела", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (NewSourceType != "CSV Import" && string.IsNullOrWhiteSpace(NewSourceToken))
            {
                MessageBox.Show("Введіть API токен", "Помилка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var source = new DataSource
            {
                Name = NewSourceName,
                Type = NewSourceType,
                ApiToken = NewSourceToken,
                ClientId = NewSourceClientId,
                ClientSecret = NewSourceClientSecret,
                IsEnabled = true
            };

            await _db.AddDataSource(source);
            await LoadSources();
            IsAddingSource = false;

            MessageBox.Show("Джерело даних додано успішно!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingSource = false;
        }

        [RelayCommand]
        private void DeleteSource(DataSource source)
        {
            var result = MessageBox.Show(
                $"Видалити джерело '{source.Name}'?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                _db.DeleteDataSource(source.Id);
                LoadSources();
            }
        }

        [RelayCommand]
        private void ToggleSource(DataSource source)
        {
            source.IsEnabled = !source.IsEnabled;
            _db.UpdateDataSource(source);
            LoadSources();
        }

[RelayCommand]
private void SyncSource(DataSource source)
{
    try
    {
        MessageBox.Show("Синхронізація запущена...", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);
        
        // Тут буде виклик відповідного API
        switch (source.Type)
        {
            case "Monobank":
                // await SyncMonobank(source);
                break;
            case "PrivatBank":
                // await SyncPrivatBank(source);
                break;
            case "Ukrsibbank":
                // await SyncUkrsibbank(source);
                break;
        }

        source.LastSync = DateTime.Now;
        _db.UpdateDataSource(source);
        LoadSources();

        MessageBox.Show("Синхронізація завершена!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Помилка синхронізації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

        [RelayCommand]
        private void ImportCsv()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV файли (*.csv)|*.csv|Всі файли (*.*)|*.*",
                Title = "Виберіть CSV файл для імпорту"
            };

            if (dialog.ShowDialog() == true)
            {
                var bankType = "universal";
                
                // Визначаємо тип банку з назви файлу
                var fileName = dialog.SafeFileName.ToLower();
                if (fileName.Contains("mono")) bankType = "monobank";
                else if (fileName.Contains("privat")) bankType = "privatbank";
                else if (fileName.Contains("ukrsib")) bankType = "ukrsibbank";

                var result = _csvImport.ImportFromCsv(dialog.FileName, bankType);

                if (result.Errors.Any())
                {
                    var details = string.Join("\n", result.Errors.Take(5));
                    MessageBox.Show($"Помилка імпорту: {details}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                MessageBox.Show(
                    $"Імпортовано: {result.Imported}\nПропущено: {result.Skipped}\nФормат: {result.Format}\nКодування: {result.EncodingUsed}",
                    "Імпорт завершено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
        }
    }
}





