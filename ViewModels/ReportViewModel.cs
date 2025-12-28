using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using doc_bursa.Models;
using doc_bursa.Services;

namespace doc_bursa.ViewModels
{
    public partial class ReportViewModel : ObservableObject
    {
        private readonly ReportService _reportService;
        private readonly ExportService _exportService;
        private readonly ReportGenerationEngine _generationEngine;

        [ObservableProperty]
        private ReportType selectedReportType = ReportType.MonthlyIncomeExpense;

        [ObservableProperty]
        private ExportFormat selectedFormat = ExportFormat.Csv;

        [ObservableProperty]
        private DateTime fromDate = DateTime.UtcNow.AddMonths(-1);

        [ObservableProperty]
        private DateTime toDate = DateTime.UtcNow;

        [ObservableProperty]
        private string? selectedCategory;

        [ObservableProperty]
        private string? selectedAccount;

        [ObservableProperty]
        private int? masterGroupId;

        [ObservableProperty]
        private string previewContent = string.Empty;

        public ObservableCollection<ReportType> ReportTypes { get; } = new(Enum.GetValues<ReportType>());

        public ObservableCollection<ExportFormat> ExportFormats { get; } = new(Enum.GetValues<ExportFormat>());

        public ObservableCollection<string> Columns { get; } = new();

        public ReportViewModel()
        {
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var categorization = new CategorizationService(db);
            var transactionService = new TransactionService(db, dedup, categorization);
            var budgetService = new BudgetService(db, transactionService, categorization);

            _reportService = new ReportService(transactionService, budgetService, categorization);
            _exportService = new ExportService(db);
            _generationEngine = new ReportGenerationEngine(db);
        }

        [RelayCommand]
        private async Task GenerateReportAsync()
        {
            var request = BuildRequest();
            var report = _reportService.GenerateReport(request);
            SyncColumns(report);

            var document = _generationEngine.BuildDocument(report);
            PreviewContent = string.Join(Environment.NewLine + Environment.NewLine, document.Pages.Select(p => p.Content));
        }

        [RelayCommand]
        private async Task DownloadReportAsync()
        {
            var request = BuildRequest();
            var report = _reportService.GenerateReport(request);
            SyncColumns(report);

            var fileName = $"Report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{SelectedFormat.ToString().ToLower()}";
            var path = Path.Combine(Path.GetTempPath(), fileName);
            var options = new ExportOptions
            {
                SelectedColumns = Columns.ToList(),
                MasterGroupId = MasterGroupId
            };

            await _exportService.ExportReportAsync(report, path, SelectedFormat, options);
            PreviewContent = $"Файл збережено: {path}";
        }

        private ReportRequest BuildRequest()
        {
            return new ReportRequest
            {
                Type = SelectedReportType,
                From = FromDate,
                To = ToDate,
                Category = SelectedCategory,
                Account = SelectedAccount,
                MasterGroupId = MasterGroupId,
                Columns = Columns.ToList(),
                PreferredFormat = SelectedFormat
            };
        }

        private void SyncColumns(ReportResult report)
        {
            if (report.Rows.Count == 0)
            {
                return;
            }

            var firstRow = report.Rows.First();
            Columns.Clear();
            foreach (var column in firstRow.Columns.Keys)
            {
                Columns.Add(column);
            }
        }
    }
}
