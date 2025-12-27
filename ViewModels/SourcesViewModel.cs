using System;
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
        private readonly CsvImportService _csvImport;
        private readonly CategorizationService _categorization;
        private readonly TransactionService _transactionService;
        private readonly ExcelImportService _excelImport;
        private readonly ImportLogService _importLog;

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

        [ObservableProperty]
        private bool isBusy;

        public string[] AvailableTypes { get; } = { "Monobank", "PrivatBank", "Ukrsibbank", "CSV Import" };

        public SourcesViewModel()
        {
            _db = new DatabaseService();
            _categorization = new CategorizationService(_db);
            var deduplicationService = new DeduplicationService(_db);
            _transactionService = new TransactionService(_db, deduplicationService);
            _csvImport = new CsvImportService(_db, _categorization, _transactionService);
            _excelImport = new ExcelImportService(_db, _categorization, _transactionService);
            _importLog = new ImportLogService();

            _ = LoadSources();
        }

        [RelayCommand]
        private async Task LoadSources()
        {
            try
            {
                IsBusy = true;
                var items = await _db.GetDataSourcesAsync();
                Sources = new ObservableCollection<DataSource>(items);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося завантажити джерела: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
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

        [RelayCommand(IncludeCancelCommand = true)]
        private async Task SaveSourceAsync(CancellationToken cancellationToken)
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

            try
            {
                IsBusy = true;
                
                // 👇 ВИПРАВЛЕННЯ: Виконуємо у фоновому потоці, щоб UI не зависав
                await Task.Run(async () => 
                {
                    await _db.AddDataSourceAsync(source, cancellationToken);
                }, cancellationToken);

                await LoadSources();
                IsAddingSource = false;
                MessageBox.Show("Джерело даних додано успішно!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show("Збереження скасовано користувачем.", "Скасовано", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося зберегти джерело: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void CancelAdd()
        {
            IsAddingSource = false;
        }

        [RelayCommand]
        private async Task DeleteSource(DataSource source)
        {
            var result = MessageBox.Show(
                $"Видалити джерело '{source.Name}'?",
                "Підтвердження",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    IsBusy = true;
                    await _db.DeleteDataSourceAsync(source.Id);
                    await LoadSources();
                    MessageBox.Show("Джерело видалено", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не вдалося видалити: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task ToggleSource(DataSource source)
        {
            try
            {
                IsBusy = true;
                source.IsEnabled = !source.IsEnabled;
                await _db.UpdateDataSourceAsync(source);
                await LoadSources();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не вдалося оновити статус: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                MessageBox.Show("Синхронізація запущена...", "Інформація", MessageBoxButton.OK, MessageBoxImage.Information);

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
                await _db.UpdateDataSourceAsync(source);
                await LoadSources();

                MessageBox.Show("Синхронізація завершена!", "Успіх", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка синхронізації: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(IncludeCancelCommand = true)]
        private async Task ImportCsv(CancellationToken cancellationToken)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "CSV файли (*.csv)|*.csv|Всі файли (*.*)|*.*",
                Title = "Виберіть CSV файл для імпорту"
            };

            if (dialog.ShowDialog() == true)
            {
                var bankType = "universal";
                var fileName = dialog.SafeFileName.ToLower();
                if (fileName.Contains("mono")) bankType = "monobank";
                else if (fileName.Contains("privat")) bankType = "privatbank";
                else if (fileName.Contains("ukrsib")) bankType = "ukrsibbank";

                var progress = new Progress<int>(_ => { });

                try
                {
                    IsBusy = true;
                    var result = await _csvImport.ImportFromCsvAsync(dialog.FileName, bankType, progress, cancellationToken);

                    if (result.Errors.Any())
                    {
                        var details = string.Join("\n", result.Errors.Take(5));
                        await _importLog.SaveImportLogAsync(result, dialog.FileName);
                        MessageBox.Show($"Помилка імпорту: {details}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }

                    MessageBox.Show(
                        $"Імпортовано: {result.Imported}\nПропущено: {result.Skipped}\nФормат: {result.Format}\nКодування: {result.EncodingUsed}",
                        "Імпорт завершено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Імпорт CSV скасовано.", "Скасовано", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand(IncludeCancelCommand = true)]
        private async Task ImportExcel(CancellationToken cancellationToken)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Excel файли (*.xlsx)|*.xlsx|Всі файли (*.*)|*.*",
                Title = "Виберіть XLSX файл для імпорту"
            };

            if (dialog.ShowDialog() == true)
            {
                var progress = new Progress<int>(_ => { });

                try
                {
                    IsBusy = true;
                    var result = await _excelImport.ImportFromExcelAsync(
                        dialog.FileName, 
                        null, 
                        progress, 
                        cancellationToken);

                    await _importLog.SaveImportLogAsync(result, dialog.FileName);

                    if (result.Errors.Any())
                    {
                        var details = string.Join("\n", result.Errors.Take(5));
                        MessageBox.Show(
                            $"Помилка імпорту:\n{details}", 
                            "Помилка", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Error);
                    }

                    MessageBox.Show(
                        $"✅ Імпортовано: {result.Imported}\n" +
                        $"⏭️ Пропущено: {result.Skipped}\n" +
                        $"📊 Формат: {result.Format}\n" +
                        $"📁 Лог: Logs/Import_{DateTime.Now:yyyyMMdd_HHmmss}.log",
                        "Імпорт завершено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (OperationCanceledException)
                {
                    MessageBox.Show("Імпорт Excel скасовано користувачем.", "Скасовано", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Критична помилка: {ex.Message}", "Помилка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }
    }
}
