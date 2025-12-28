using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

        /// <summary>
        /// Створити планову транзакцію.
        /// </summary>
        public async Task<PlannedTransaction> CreatePlannedTransactionAsync(
            PlannedTransaction plannedTransaction, CancellationToken ct = default)
        {
            using var db = _databaseService.CreateDbContext();
            db.PlannedTransactions.Add(plannedTransaction);
            await db.SaveChangesAsync(ct);
            _logger.Information("Created planned transaction {Id}", plannedTransaction.Id);
            return plannedTransaction;
        }

        /// <summary>
        /// Отримати всі планові транзакції для рахунку.
        /// </summary>
        public List<PlannedTransaction> GetPlannedTransactions(string accountNumber)
        {
            using var db = _databaseService.CreateDbContext();
            return db.PlannedTransactions
                .Where(pt => pt.AccountNumber == accountNumber && !pt.IsExecuted)
                .OrderBy(pt => pt.PlannedDate)
                .ToList();
        }

        /// <summary>
        /// Позначити планову транзакцію як виконану.
        /// </summary>
        public async Task MarkAsExecutedAsync(int plannedTransactionId, int actualTransactionId, CancellationToken ct = default)
        {
            using var db = _databaseService.CreateDbContext();
            var planned = db.PlannedTransactions.FirstOrDefault(pt => pt.Id == plannedTransactionId);
            if (planned != null)
            {
                planned.IsExecuted = true;
                planned.ActualTransactionId = actualTransactionId;
                await db.SaveChangesAsync(ct);
                _logger.Information("Marked planned transaction {Id} as executed", plannedTransactionId);
            }
        }

        /// <summary>
        /// Розрахувати "Вільні кошти" (Free Cash) для рахунку.
        /// Free Cash = Поточний Баланс - Сума Планових Витрат (до кінця періоду).
        /// </summary>
        public decimal CalculateFreeCash(string accountNumber, DateTime endDate)
        {
            using var db = _databaseService.CreateDbContext();

            // Поточний баланс
            var account = db.Accounts.FirstOrDefault(a => a.AccountNumber == accountNumber);
            if (account == null) return 0m;

            var currentBalance = account.Balance;

            // Сума планових витрат до endDate
            var plannedExpenses = db.PlannedTransactions
                .Where(pt => pt.AccountNumber == accountNumber
                             && !pt.IsExecuted
                             && pt.PlannedDate <= endDate
                             && pt.Amount < 0)
                .Sum(pt => pt.Amount);

            return currentBalance + plannedExpenses; // plannedExpenses вже негативна
        }

        /// <summary>
        /// Отримати всі планові транзакції в діапазоні дат.
        /// </summary>
        public List<PlannedTransaction> GetPlannedTransactionsByDateRange(
            string accountNumber, DateTime startDate, DateTime endDate)
        {
            using var db = _databaseService.CreateDbContext();
            return db.PlannedTransactions
                .Where(pt => pt.AccountNumber == accountNumber
                             && pt.PlannedDate >= startDate
                             && pt.PlannedDate <= endDate)
                .OrderBy(pt => pt.PlannedDate)
                .ToList();
        }

        /// <summary>
        /// Видалити планову транзакцію.
        /// </summary>
        public async Task DeletePlannedTransactionAsync(int plannedTransactionId, CancellationToken ct = default)
        {
            using var db = _databaseService.CreateDbContext();
            var planned = db.PlannedTransactions.FirstOrDefault(pt => pt.Id == plannedTransactionId);
            if (planned != null)
            {
                db.PlannedTransactions.Remove(planned);
                await db.SaveChangesAsync(ct);
                _logger.Information("Deleted planned transaction {Id}", plannedTransactionId);
            }
        }
    }
}
