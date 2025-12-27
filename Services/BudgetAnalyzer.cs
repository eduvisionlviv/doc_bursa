using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Калькуляції витрат по категоріях та періодах.
    /// </summary>
    public class BudgetAnalyzer
    {
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorizationService;

        public BudgetAnalyzer(TransactionService transactionService, CategorizationService categorizationService)
        {
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _categorizationService = categorizationService ?? throw new ArgumentNullException(nameof(categorizationService));
        }

        /// <summary>
        /// Розрахунок витрат по категоріях за період.
        /// </summary>
        public Dictionary<string, decimal> CalculateSpendingByCategory(DateTime from, DateTime to)
        {
            var transactions = FilterTransactions(from, to);

            return transactions
                .Where(t => t.Amount < 0)
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Не визначено" : t.Category)
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
        }

        /// <summary>
        /// Аналіз конкретного бюджету.
        /// </summary>
        public BudgetAnalysisResult AnalyzeBudget(Budget budget, DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            var (periodStart, periodEnd) = ResolvePeriod(budget, from, to);
            var spent = CalculateSpendingForCategory(budget.Category, periodStart, periodEnd);
            var usage = budget.Limit > 0 ? Math.Round((spent / budget.Limit) * 100, 2) : 0;
            var remaining = budget.Limit - spent;
            var isOver = spent > budget.Limit;
            var shouldAlert = usage >= budget.AlertThreshold;
            var forecast = BuildForecast(budget, spent, periodStart, periodEnd, referenceDate);

            return new BudgetAnalysisResult
            {
                Budget = budget,
                ActualSpent = spent,
                Remaining = remaining,
                UsagePercentage = usage,
                IsOverBudget = isOver,
                ShouldAlert = shouldAlert,
                Forecast = forecast
            };
        }

        /// <summary>
        /// Місячний зріз витрат по бюджету.
        /// </summary>
        public IReadOnlyList<BudgetPeriodSummary> GetMonthlyView(Budget budget, int year)
        {
            var summaries = new List<BudgetPeriodSummary>();

            for (var month = 1; month <= 12; month++)
            {
                var start = new DateTime(year, month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                var spent = CalculateSpendingForCategory(budget.Category, start, end);
                summaries.Add(new BudgetPeriodSummary
                {
                    PeriodLabel = $"{year}-{month:D2}",
                    Spent = spent,
                    UsagePercentage = budget.Limit > 0 ? Math.Round((spent / budget.Limit) * 100, 2) : 0
                });
            }

            return summaries;
        }

        /// <summary>
        /// Річний зріз витрат за бюджетом.
        /// </summary>
        public IReadOnlyList<BudgetPeriodSummary> GetYearlyView(Budget budget, int startYear, int years)
        {
            var summaries = new List<BudgetPeriodSummary>();

            for (var year = startYear; year < startYear + years; year++)
            {
                var start = new DateTime(year, 1, 1);
                var end = start.AddYears(1).AddDays(-1);
                var spent = CalculateSpendingForCategory(budget.Category, start, end);

                summaries.Add(new BudgetPeriodSummary
                {
                    PeriodLabel = year.ToString(),
                    Spent = spent,
                    UsagePercentage = budget.Limit > 0 ? Math.Round((spent / budget.Limit) * 100, 2) : 0
                });
            }

            return summaries;
        }

        /// <summary>
        /// Розрахунок витрат по конкретній категорії за період.
        /// </summary>
        public decimal CalculateSpendingForCategory(string category, DateTime from, DateTime to)
        {
            var transactions = FilterTransactions(from, to)
                .Where(t => string.IsNullOrWhiteSpace(category)
                    ? true
                    : string.Equals(t.Category, category, StringComparison.OrdinalIgnoreCase));

            return transactions
                .Where(t => t.Amount < 0)
                .Sum(t => Math.Abs(t.Amount));
        }

        /// <summary>
        /// Аналіз кількох бюджетів одразу.
        /// </summary>
        public IReadOnlyDictionary<Guid, BudgetAnalysisResult> AnalyzeBudgets(IEnumerable<Budget> budgets, DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            var results = new Dictionary<Guid, BudgetAnalysisResult>();

            foreach (var budget in budgets)
            {
                var analysis = AnalyzeBudget(budget, from, to, referenceDate);
                results[budget.Id] = analysis;
            }

            return results;
        }

        private IEnumerable<Transaction> FilterTransactions(DateTime from, DateTime to)
        {
            var transactions = _transactionService.GetTransactions()
                .Where(t => t.Date >= from && t.Date <= to)
                .ToList();

            foreach (var tx in transactions.Where(t => string.IsNullOrWhiteSpace(t.Category)))
            {
                tx.Category = _categorizationService.CategorizeTransaction(tx);
            }

            return transactions;
        }

        private (DateTime start, DateTime end) ResolvePeriod(Budget budget, DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue)
            {
                return (from.Value, to.Value);
            }

            var start = budget.StartDate == default ? DateTime.UtcNow.Date : budget.StartDate;
            DateTime end = budget.Frequency switch
            {
                BudgetFrequency.Weekly => start.AddDays(6),
                BudgetFrequency.Quarterly => start.AddMonths(3).AddDays(-1),
                BudgetFrequency.Yearly => start.AddYears(1).AddDays(-1),
                _ => start.AddMonths(1).AddDays(-1)
            };

            if (budget.EndDate.HasValue && budget.EndDate.Value < end)
            {
                end = budget.EndDate.Value;
            }

            return (start, end);
        }

        private BudgetForecast BuildForecast(Budget budget, decimal spent, DateTime start, DateTime end, DateTime? referenceDate)
        {
            var today = referenceDate?.Date ?? DateTime.UtcNow.Date;
            var cappedToday = today > end ? end : today;
            var elapsed = Math.Max(1, (cappedToday - start).TotalDays + 1);
            var totalDays = Math.Max(elapsed, (end - start).TotalDays + 1);
            var averagePerDay = elapsed > 0 ? spent / (decimal)elapsed : 0;
            var projected = averagePerDay * (decimal)totalDays;
            var projectedUsage = budget.Limit > 0 ? Math.Round((projected / budget.Limit) * 100, 2) : 0;

            return new BudgetForecast
            {
                ProjectedAmount = Math.Round(projected, 2),
                ProjectedUsagePercentage = projectedUsage
            };
        }
    }
}
