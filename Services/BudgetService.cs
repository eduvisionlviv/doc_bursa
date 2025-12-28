using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;
using Microsoft.Data.Sqlite;
using Serilog;

namespace doc_bursa.Services
{
    public class BudgetService
    {
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly CategorizationService _categorizationService;

        public BudgetService(DatabaseService databaseService, TransactionService transactionService, CategorizationService categorizationService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _categorizationService = categorizationService ?? throw new ArgumentNullException(nameof(categorizationService));
        }

        public void CreateBudget(Budget budget)
        {
            _databaseService.SaveBudget(budget);
        }

        public void DeleteBudget(Guid budgetId)
        {
            _databaseService.DeleteBudget(budgetId);
        }

        public List<Budget> GetBudgets()
        {
            return _databaseService.GetBudgets();
        }

        // Overload for ReportService - takes 4 parameters and returns BudgetAnalysisResult
        public BudgetAnalysisResult EvaluateBudget(Budget budget, DateTime? from, DateTime? to, DateTime currentDate)
        {
            var startDate = from ?? budget.StartDate;
            var endDate = to ?? budget.EndDate ?? currentDate;
            var transactions = _transactionService.GetTransactions(startDate, endDate, budget.Category, null);
            var spent = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            
            return new BudgetAnalysisResult
            {
                Budget = budget,
                ActualSpent = spent,
                UsagePercentage = budget.Limit > 0 ? (spent / budget.Limit) * 100 : 0
            };
        }

        // For BudgetViewModel - returns Dictionary
        public Dictionary<Guid, BudgetAnalysisResult> EvaluateAllBudgets()
        {
            var budgets = GetBudgets();
            var now = DateTime.Now;
            var results = new Dictionary<Guid, BudgetAnalysisResult>();

            foreach (var budget in budgets.Where(b => b.IsActive))
            {
                var startDate = budget.StartDate;
                var endDate = budget.EndDate ?? now;
                var analysis = EvaluateBudget(budget, startDate, endDate, now);
                results[budget.Id] = analysis;
            }

            return results;
        }

        // For BudgetViewModel - returns List<BudgetAlert>
        public List<BudgetAlert> GetAlerts()
        {
            var alerts = new List<BudgetAlert>();
            var budgets = GetBudgets().Where(b => b.IsActive);

            foreach (var budget in budgets)
            {
                var percentage = budget.Limit > 0 ? (budget.Spent / budget.Limit) * 100 : 0;
                if (percentage >= budget.AlertThreshold)
                {
                    alerts.Add(new BudgetAlert
                    {
                        BudgetId = budget.Id,
                        BudgetName = budget.Name,
                        Message = $"Budget '{budget.Name}': {percentage:F1}% used ({budget.Spent}/{budget.Limit})",
                        Severity = percentage >= 100 ? AlertSeverity.Critical : 
                                   percentage >= 90 ? AlertSeverity.Warning : AlertSeverity.Info,
                        Percentage = percentage
                    });
                }
            }

            return alerts;
        }
    }
}
