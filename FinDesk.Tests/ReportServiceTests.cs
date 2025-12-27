using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class ReportServiceTests : IDisposable
    {
        private readonly string _dbDirectory;
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly BudgetService _budgetService;
        private readonly CategorizationService _categorizationService;
        private readonly ReportService _reportService;
        private readonly ExportService _exportService;
        private readonly ReportGenerationEngine _engine;

        public ReportServiceTests()
        {
            _dbDirectory = Path.Combine(Path.GetTempPath(), "findesk-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dbDirectory);
            var dbPath = Path.Combine(_dbDirectory, "test.db");

            _databaseService = new DatabaseService(dbPath);
            _categorizationService = new CategorizationService(_databaseService);
            var dedup = new DeduplicationService(_databaseService);
            _transactionService = new TransactionService(_databaseService, dedup);
            _budgetService = new BudgetService(_databaseService, _transactionService, _categorizationService);
            _reportService = new ReportService(_transactionService, _budgetService, _categorizationService);
            _exportService = new ExportService();
            _engine = new ReportGenerationEngine();
        }

        [Fact]
        public void MonthlyIncomeExpense_ShouldAggregateCorrectly()
        {
            AddTransaction(DateTime.UtcNow.AddMonths(-1).AddDays(1), 1000m, "Salary");
            AddTransaction(DateTime.UtcNow.AddMonths(-1).AddDays(2), -200m, "Groceries");
            AddTransaction(DateTime.UtcNow.AddMonths(-2).AddDays(3), 500m, "Bonus");
            AddTransaction(DateTime.UtcNow.AddMonths(-2).AddDays(4), -100m, "Coffee");

            var request = new ReportRequest
            {
                Type = ReportType.MonthlyIncomeExpense,
                From = DateTime.UtcNow.AddMonths(-2).AddDays(-1),
                To = DateTime.UtcNow
            };

            var report = _reportService.GenerateReport(request);

            Assert.Equal(1500m, report.Metrics["Загальний дохід"]);
            Assert.Equal(300m, report.Metrics["Загальні витрати"]);
            Assert.Equal(1200m, report.Metrics["Сальдо"]);
            Assert.True(report.Rows.Count >= 2);
        }

        [Fact]
        public void CategoryBreakdown_ShouldRespectDateRange()
        {
            var now = DateTime.UtcNow;
            AddTransaction(now.AddDays(-2), -50m, "Cafe");
            AddTransaction(now.AddDays(-40), -500m, "Electronics");

            var request = new ReportRequest
            {
                Type = ReportType.CategoryBreakdown,
                From = now.AddDays(-7),
                To = now
            };

            var report = _reportService.GenerateReport(request);

            Assert.Single(report.Rows);
            Assert.Equal(50m, report.Rows.First()["Сума"]);
        }

        [Fact]
        public async Task ExportService_ShouldRespectSelectedColumns()
        {
            var rows = new List<ReportRow>
            {
                new ReportRow { ["Category"] = "Food", ["Amount"] = 100m, ["Count"] = 2 }
            };
            var options = new ExportOptions
            {
                SelectedColumns = new List<string> { "Category", "Amount" }
            };
            var path = Path.Combine(_dbDirectory, "columns.csv");

            var success = await _exportService.ExportToCsvAsync(rows, path, options);
            var content = await File.ReadAllTextAsync(path);
            var header = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            Assert.True(success);
            Assert.Equal("Category,Amount", header);
        }

        [Fact]
        public async Task ExportService_ShouldApplyFilters()
        {
            var report = new ReportResult
            {
                Type = ReportType.CustomRange,
                Title = "Filtered",
                From = DateTime.UtcNow.AddDays(-1),
                To = DateTime.UtcNow
            };
            var first = new ReportRow();
            first["Account"] = "A1";
            first["Amount"] = 10;
            var second = new ReportRow();
            second["Account"] = "B2";
            second["Amount"] = 20;
            report.Rows.Add(first);
            report.Rows.Add(second);

            var options = new ExportOptions
            {
                Filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "Account", "A1" } }
            };

            var path = Path.Combine(_dbDirectory, "filtered.csv");
            await _exportService.ExportReportAsync(report, path, ExportFormat.Csv, options);
            var content = await File.ReadAllTextAsync(path);
            Assert.Contains("A1", content);
            Assert.DoesNotContain("B2", content);
        }

        [Fact]
        public void ReportService_ShouldHandleLargeDataset()
        {
            var start = DateTime.UtcNow.AddDays(-10);
            for (var i = 0; i < 500; i++)
            {
                AddTransaction(start.AddMinutes(i), i % 2 == 0 ? 5 : -5, $"Tx {i}");
            }

            var report = _reportService.GenerateReport(new ReportRequest
            {
                Type = ReportType.CustomRange,
                From = start,
                To = DateTime.UtcNow
            });

            Assert.Equal(500, report.Rows.Count);
            Assert.NotNull(report.Metrics["Кількість"]);
        }

        [Fact]
        public void ReportGenerationEngine_ShouldCreateMultiplePages()
        {
            var result = new ReportResult
            {
                Type = ReportType.CustomRange,
                Title = "Paged",
                From = DateTime.UtcNow.AddDays(-1),
                To = DateTime.UtcNow
            };

            for (var i = 0; i < 25; i++)
            {
                var row = new ReportRow();
                row["Index"] = i;
                row["Value"] = i * 2;
                result.Rows.Add(row);
            }

            var doc = _engine.BuildDocument(result);
            Assert.True(doc.Pages.Count > 1);
            Assert.Equal("Paged", doc.Title);
        }

        private void AddTransaction(DateTime date, decimal amount, string description, string category = "Misc")
        {
            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid().ToString(),
                Date = date,
                Amount = amount,
                Description = description,
                Category = category,
                Account = "Test"
            };

            _transactionService.AddTransaction(transaction);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_dbDirectory, true);
            }
            catch
            {
                // ignore cleanup errors
            }
        }
    }
}

