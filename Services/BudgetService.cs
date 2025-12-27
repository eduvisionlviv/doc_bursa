using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Управління бюджетами та сповіщеннями.
    /// </summary>
    public class BudgetService
    {
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorizationService;
        private readonly BudgetAnalyzer _analyzer;

        public BudgetService(DatabaseService databaseService, TransactionService transactionService, CategorizationService categorizationService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _categorizationService = categorizationService ?? throw new ArgumentNullException(nameof(categorizationService));
            _analyzer = new BudgetAnalyzer(_transactionService, _categorizationService);
        }

        public IReadOnlyCollection<Budget> GetBudgets()
        {
            return _databaseService.GetBudgets();
        }

        public Budget? GetBudget(Guid id)
        {
            return _databaseService.GetBudget(id);
        }

        public Budget CreateBudget(Budget budget)
        {
            if (budget.Id == Guid.Empty)
            {
                budget.Id = Guid.NewGuid();
            }

            budget.CreatedAt = DateTime.UtcNow;
            budget.UpdatedAt = DateTime.UtcNow;
            budget.StartDate = budget.StartDate == default ? DateTime.UtcNow.Date : budget.StartDate.Date;
            _databaseService.SaveBudget(budget);
            return budget;
        }

        public bool UpdateBudget(Budget budget)
        {
            if (_databaseService.GetBudget(budget.Id) == null)
            {
                return false;
            }

            budget.UpdatedAt = DateTime.UtcNow;
            _databaseService.SaveBudget(budget);
            return true;
        }

        public bool DeleteBudget(Guid id)
        {
            if (_databaseService.GetBudget(id) == null)
            {
                return false;
            }

            _databaseService.DeleteBudget(id);
            return true;
        }

        public BudgetAnalysisResult EvaluateBudget(Guid id, DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            var budget = _databaseService.GetBudget(id);
            if (budget == null)
            {
                throw new InvalidOperationException("Бюджет не знайдено");
            }

            return EvaluateBudget(budget, from, to, referenceDate);
        }

        public BudgetAnalysisResult EvaluateBudget(Budget budget, DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            var result = _analyzer.AnalyzeBudget(budget, from, to, referenceDate);
            budget.Spent = result.ActualSpent;
            budget.UpdatedAt = DateTime.UtcNow;
            _databaseService.SaveBudget(budget);
            return result;
        }

        public IReadOnlyDictionary<Guid, BudgetAnalysisResult> EvaluateAllBudgets(DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            var results = new Dictionary<Guid, BudgetAnalysisResult>();
            var budgets = _databaseService.GetBudgets().Where(b => b.IsActive).ToList();

            foreach (var budget in budgets)
            {
                var analysis = EvaluateBudget(budget, from, to, referenceDate);
                results[budget.Id] = analysis;
            }

            return results;
        }

        public IReadOnlyList<BudgetAlert> GetAlerts(DateTime? from = null, DateTime? to = null, DateTime? referenceDate = null)
        {
            var alerts = new List<BudgetAlert>();
            var budgets = _databaseService.GetBudgets().Where(b => b.IsActive);

            foreach (var budget in budgets)
            {
                var analysis = EvaluateBudget(budget, from, to, referenceDate);
                if (analysis.ShouldAlert || analysis.IsOverBudget)
                {
                    alerts.Add(new BudgetAlert
                    {
                        BudgetId = budget.Id,
                        BudgetName = budget.Name,
                        Category = budget.Category,
                        UsagePercentage = analysis.UsagePercentage,
                        Limit = budget.Limit,
                        Spent = analysis.ActualSpent,
                        IsOverBudget = analysis.IsOverBudget,
                        Message = analysis.IsOverBudget
                            ? "Перевищено ліміт бюджету"
                            : $"Досягнуто {analysis.UsagePercentage}% ліміту"
                    });
                }
            }

            return alerts;
        }

        public bool AddTransactionWithBudgeting(Transaction transaction)
        {
            if (transaction == null)
            {
                throw new ArgumentNullException(nameof(transaction));
            }

            if (string.IsNullOrWhiteSpace(transaction.Category))
            {
                transaction.Category = _categorizationService.CategorizeTransaction(transaction);
            }

            var added = _transactionService.AddTransaction(transaction);
            if (!added)
            {
                return false;
            }

            var relatedBudgets = _databaseService.GetBudgets()
                .Where(b => b.IsActive && string.Equals(b.Category, transaction.Category, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var budget in relatedBudgets)
            {
                EvaluateBudget(budget);
            }

            return true;
        }
    }
}
