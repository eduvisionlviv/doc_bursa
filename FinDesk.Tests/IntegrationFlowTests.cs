using System;
using System.IO;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class IntegrationFlowTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DatabaseService _db;
        private readonly DeduplicationService _dedup;
        private readonly TransactionService _tx;
        private readonly BudgetService _budget;
        private readonly CategorizationService _categorization;
        private readonly ExportService _export;

        public IntegrationFlowTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _db = new DatabaseService(_dbPath);
            _dedup = new DeduplicationService(_db, t => t.TransactionId);
            _tx = new TransactionService(_db, _dedup);
            _categorization = new CategorizationService(_db);
            _budget = new BudgetService(_db, _tx, _categorization);
            _export = new ExportService();
        }

        [Fact]
        public async Task ImportToAnalyticsToExport_Flow_Works()
        {
            _tx.AddTransaction(new Transaction
            {
                TransactionId = "flow-1",
                Date = DateTime.UtcNow,
                Amount = -100,
                Category = "Продукти",
                Description = "Integration flow"
            });

            var budget = _budget.CreateBudget(new Budget
            {
                Name = "Food",
                Category = "Продукти",
                Limit = 500m,
                Frequency = BudgetFrequency.Monthly
            });

            var analysis = _budget.EvaluateBudget(budget.Id);
            Assert.True(analysis.UsagePercentage > 0);

            var rows = new[]
            {
                new ReportRow { ["Category"] = "Продукти", ["Amount"] = 100 }
            };
            var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.csv");
            var exported = await _export.ExportToCsvAsync(rows, tempFile, new ExportOptions());

            Assert.True(exported);
            Assert.True(File.Exists(tempFile));
            File.Delete(tempFile);
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
