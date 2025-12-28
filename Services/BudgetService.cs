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
        private readonly ILogger _logger;

        public BudgetService(DatabaseService databaseService, TransactionService transactionService, ILogger logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void CreateBudget(Budget budget)
        {
            _databaseService.SaveBudget(budget);
            _logger.Information("Created budget {BudgetId}", budget.Id);
        }

        public void DeleteBudget(Guid budgetId)
        {
            _databaseService.DeleteBudget(budgetId);
            _logger.Information("Deleted budget {BudgetId}", budgetId);
        }

        public List<Budget> GetBudgets()
        {
            return _databaseService.GetBudgets();
        }

        public void EvaluateBudget(Budget budget, DateTime startDate, DateTime endDate)
        {
            var transactions = _transactionService.GetTransactions(startDate, endDate, budget.Category, null);
            budget.Spent = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            _databaseService.SaveBudget(budget);
        }

        public void EvaluateAllBudgets()
        {
            var budgets = GetBudgets();
            var now = DateTime.Now;
            
            foreach (var budget in budgets.Where(b => b.IsActive))
            {
                var startDate = budget.StartDate;
                var endDate = budget.EndDate ?? now;
                EvaluateBudget(budget, startDate, endDate);
            }
        }

        public List<string> GetAlerts()
        {
            var alerts = new List<string>();
            var budgets = GetBudgets().Where(b => b.IsActive);
            
            foreach (var budget in budgets)
            {
                var percentage = budget.Limit > 0 ? (budget.Spent / budget.Limit) * 100 : 0;
                if (percentage >= budget.AlertThreshold)
                {
                    alerts.Add($"Budget '{budget.Name}': {percentage:F1}% used ({budget.Spent}/{budget.Limit})");
                }
            }
            
            return alerts;
        }
    }
}
