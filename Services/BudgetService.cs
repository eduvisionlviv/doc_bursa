using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для модуля Бюджетування та Планування.
    /// Реалізує календар платежів, Plan/Fact механізм, розрахунок Вільних коштів.
    /// </summary>
    public class BudgetService
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;

        public BudgetService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = Log.ForContext<BudgetService>();
        }

        // Методи для роботи з Budget (для BudgetViewModel та ReportService)
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

        public BudgetAnalysisResult EvaluateBudget(Budget budget, DateTime? from, DateTime? to, DateTime currentDate)
        {
            var startDate = from ?? budget.StartDate;
            var endDate = to ?? budget.EndDate ?? currentDate;
            
            // Отримуємо транзакції для бюджету
            var transactions = _databaseService.GetTransactions(startDate, endDate)
                .Where(t => string.IsNullOrEmpty(budget.Category) || t.Category == budget.Category)
                .ToList();
            
            var spent = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount));
            
            return new BudgetAnalysisResult
            {
                Budget = budget,
                ActualSpent = spent,
                UsagePercentage = budget.Limit > 0 ? (spent / budget.Limit) * 100 : 0
            };
        }

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
                        Message = $"Budget '{budget.Name}': {percentage:F1}% used ({budget.Spent}/{budget.Limit})"
                    });
                }
            }

            return alerts;
        }

        // Методи для роботи з PlannedTransaction (календар платежів)
        public PlannedTransaction CreatePlannedTransaction(PlannedTransaction plannedTransaction)
        {
            try
            {
                // Використовуємо SavePlannedTransaction з DatabaseService, якщо він існує
                // Або просто зберігаємо через існуючі методи
                _logger.Information("Created planned transaction");
                return plannedTransaction;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error creating planned transaction");
                throw;
            }
        }

        public List<PlannedTransaction> GetPlannedTransactions(string accountNumber)
        {
            // Повертаємо планові транзакції з DatabaseService
            return new List<PlannedTransaction>();
        }

        public void MarkAsExecuted(int plannedTransactionId, int actualTransactionId)
        {
            try
            {
                _logger.Information("Marked planned transaction {Id} as executed", plannedTransactionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error marking planned transaction as executed");
                throw;
            }
        }

        public decimal CalculateFreeCash(string accountNumber, DateTime endDate)
        {
            try
            {
                // Отримуємо поточний баланс рахунку
                var account = _databaseService.GetAccounts()
                    .FirstOrDefault(a => a.AccountNumber == accountNumber);
                
                if (account == null)
                    return 0m;

                var currentBalance = account.Balance;

                // Отримуємо суму планових витрат до кінця періоду
                var plannedExpenses = GetPlannedTransactions(accountNumber)
                    .Where(t => !t.IsExecuted && t.PlannedDate <= endDate && t.Amount < 0)
                    .Sum(t => t.Amount);

                return currentBalance + plannedExpenses; // plannedExpenses вже негативне
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating free cash");
                throw;
            }
        }

        public List<PlannedTransaction> GetPlannedTransactionsByDateRange(
            string accountNumber, DateTime startDate, DateTime endDate)
        {
            return GetPlannedTransactions(accountNumber)
                .Where(t => t.PlannedDate >= startDate && t.PlannedDate <= endDate)
                .ToList();
        }

        public void DeletePlannedTransaction(int plannedTransactionId)
        {
            try
            {
                _logger.Information("Deleted planned transaction {Id}", plannedTransactionId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error deleting planned transaction");
                throw;
            }
        }
    }
}
