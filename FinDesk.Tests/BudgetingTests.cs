using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class BudgetingTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorizationService;
        private readonly BudgetService _budgetService;
        private readonly BudgetAnalyzer _analyzer;

        public BudgetingTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _databaseService = new DatabaseService(_dbPath);
            var deduplicationService = new DeduplicationService(_databaseService, t => t.TransactionId);
            _categorizationService = new CategorizationService(_databaseService);
            _transactionService = new TransactionService(_databaseService, deduplicationService);
            _budgetService = new BudgetService(_databaseService, _transactionService, _categorizationService);
            _analyzer = new BudgetAnalyzer(_transactionService, _categorizationService);
        }

        [Fact]
        public void BudgetService_MarksOverBudgetWhenLimitExceeded()
        {
            var budget = _budgetService.CreateBudget(new Budget
            {
                Name = "Food",
                Category = "Продукти",
                Limit = 500m,
                AlertThreshold = 70,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1)
            });

            AddTransaction("t1", -300m, "Продукти", new DateTime(2025, 1, 5));
            AddTransaction("t2", -250m, "Продукти", new DateTime(2025, 1, 10));

            var analysis = _budgetService.EvaluateBudget(budget.Id, new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

            Assert.True(analysis.IsOverBudget);
            Assert.True(analysis.UsagePercentage > 100);
            Assert.Equal(550m, analysis.ActualSpent);
        }

        [Fact]
        public void BudgetService_RaisesAlertWhenThresholdReached()
        {
            var budget = _budgetService.CreateBudget(new Budget
            {
                Name = "Transport",
                Category = "Транспорт",
                Limit = 1000m,
                AlertThreshold = 50,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1)
            });

            AddTransaction("t3", -600m, "Транспорт", new DateTime(2025, 1, 3));

            var alerts = _budgetService.GetAlerts(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31));

            Assert.Contains(alerts, a => a.BudgetId == budget.Id && a.UsagePercentage >= 50);
        }

        [Fact]
        public void BudgetAnalyzer_CalculatesSpendingByCategory()
        {
            AddTransaction("t4", -200m, "Продукти", new DateTime(2025, 2, 1));
            AddTransaction("t5", -100m, "Транспорт", new DateTime(2025, 2, 2));
            AddTransaction("t6", 500m, "Дохід", new DateTime(2025, 2, 3));

            var spending = _analyzer.CalculateSpendingByCategory(new DateTime(2025, 2, 1), new DateTime(2025, 2, 28));

            Assert.Equal(200m, spending["Продукти"]);
            Assert.Equal(100m, spending["Транспорт"]);
            Assert.False(spending.ContainsKey("Дохід"));
        }

        [Fact]
        public void BudgetAnalyzer_ComputesMonthlyAndYearlyViews()
        {
            var budget = _budgetService.CreateBudget(new Budget
            {
                Name = "Monthly Food",
                Category = "Продукти",
                Limit = 1000m,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1)
            });

            AddTransaction("t7", -200m, "Продукти", new DateTime(2025, 1, 15));
            AddTransaction("t8", -300m, "Продукти", new DateTime(2025, 2, 10));

            var monthly = _analyzer.GetMonthlyView(budget, 2025);
            var january = monthly.First(m => m.PeriodLabel == "2025-01");
            var february = monthly.First(m => m.PeriodLabel == "2025-02");

            Assert.Equal(200m, january.Spent);
            Assert.Equal(20m, january.UsagePercentage);
            Assert.Equal(300m, february.Spent);

            var yearly = _analyzer.GetYearlyView(budget, 2025, 1);
            Assert.Single(yearly);
            Assert.Equal(50m, yearly.First().UsagePercentage);
        }

        [Fact]
        public void BudgetService_EvaluatesMultipleBudgetsIndependently()
        {
            var foodBudget = _budgetService.CreateBudget(new Budget
            {
                Name = "Food",
                Category = "Продукти",
                Limit = 500m,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 3, 1)
            });

            var travelBudget = _budgetService.CreateBudget(new Budget
            {
                Name = "Travel",
                Category = "Подорожі",
                Limit = 1200m,
                Frequency = BudgetFrequency.Monthly,
                StartDate = new DateTime(2025, 3, 1)
            });

            AddTransaction("t9", -200m, "Продукти", new DateTime(2025, 3, 5));
            AddTransaction("t10", -500m, "Подорожі", new DateTime(2025, 3, 6));

            var analyses = _budgetService.EvaluateAllBudgets(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));

            Assert.Equal(40m, analyses[foodBudget.Id].UsagePercentage);
            Assert.Equal(41.67m, analyses[travelBudget.Id].UsagePercentage);
        }

        private void AddTransaction(string transactionId, decimal amount, string category, DateTime date)
        {
            _transactionService.AddTransaction(new Transaction
            {
                TransactionId = transactionId,
                Amount = amount,
                Category = category,
                Date = date,
                Description = $"{category} expense"
            });
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
