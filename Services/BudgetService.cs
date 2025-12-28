using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using doc_bursa.Infrastructure.Data;
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
            PlannedTransaction planned,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            context.PlannedTransactions.Add(planned);
            await context.SaveChangesAsync(ct);
            _logger.Information("Created planned transaction: {Name} on {Date}", planned.Name, planned.PlannedDate);
            return planned;
        }

        /// <summary>
        /// Отримати планові транзакції за період.
        /// </summary>
        public async Task<List<PlannedTransaction>> GetPlannedTransactionsAsync(
            DateTime from,
            DateTime to,
            int? accountId = null,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            var query = context.PlannedTransactions
                .Where(p => p.PlannedDate >= from && p.PlannedDate <= to);

            if (accountId.HasValue)
            {
                query = query.Where(p => p.AccountId == accountId.Value);
            }

            return query.OrderBy(p => p.PlannedDate).ToList();
        }

        /// <summary>
        /// Генерація планових транзакцій з шаблонів регулярних платежів.
        /// </summary>
        public async Task GeneratePlannedTransactionsFromTemplatesAsync(
            DateTime from,
            DateTime to,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            var templates = context.RecurringTransactions
                .Where(r => r.IsActive && r.NextDueDate >= from && r.NextDueDate <= to)
                .ToList();

            foreach (var template in templates)
            {
                // Перевіряємо, чи вже існує планова транзакція з цього шаблону
                var exists = context.PlannedTransactions
                    .Any(p =>
                        p.RecurringTransactionId == template.Id &&
                        p.PlannedDate.Date == template.NextDueDate.Date);

                if (!exists)
                {
                    var planned = new PlannedTransaction
                    {
                        Name = template.Description,
                        Description = template.Notes,
                        PlannedDate = template.NextDueDate,
                        Amount = template.Amount,
                        Category = template.Category,
                        AccountId = template.AccountId,
                        RecurringTransactionId = template.Id,
                        IsRecurring = true,
                        Status = PlannedTransactionStatus.Pending
                    };

                    context.PlannedTransactions.Add(planned);
                    _logger.Information(
                        "Generated planned transaction from template: {Description} on {Date}",
                        template.Description, template.NextDueDate);
                }
            }

            await context.SaveChangesAsync(ct);
        }

        /// <summary>
        /// Позначити планову транзакцію як виконану (поглинуту реальною).
        /// </summary>
        public async Task MarkPlannedAsCompletedAsync(
            int plannedId,
            int actualTransactionId,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            var planned = context.PlannedTransactions.FirstOrDefault(p => p.Id == plannedId);

            if (planned != null)
            {
                planned.Status = PlannedTransactionStatus.Completed;
                planned.ActualTransactionId = actualTransactionId;
                await context.SaveChangesAsync(ct);
                _logger.Information(
                    "Marked planned transaction {PlannedId} as completed by actual {ActualId}",
                    plannedId, actualTransactionId);
            }
        }

        /// <summary>
        /// Розрахунок Вільних коштів (Free Cash).
        /// Формула: Поточний Баланс - Сума Планових Витрат (до кінця періоду).
        /// </summary>
        public async Task<decimal> CalculateFreeCashAsync(
            int accountId,
            DateTime periodEnd,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();

            // Поточний баланс рахунку
            var account = context.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (account == null)
            {
                return 0m;
            }

            var currentBalance = account.Balance;

            // Сума планових витрат до periodEnd
            var plannedExpenses = context.PlannedTransactions
                .Where(p =>
                    p.AccountId == accountId &&
                    p.Status == PlannedTransactionStatus.Pending &&
                    p.PlannedDate <= periodEnd &&
                    p.Amount < 0) // Тільки витрати
                .Sum(p => p.Amount);

            var freeCash = currentBalance + plannedExpenses; // plannedExpenses вже негативне

            _logger.Debug(
                "Free cash for account {AccountId}: Balance={Balance}, Planned={Planned}, Free={Free}",
                accountId, currentBalance, plannedExpenses, freeCash);

            return freeCash;
        }

        /// <summary>
        /// Автоматичне поглинання планових транзакцій реальними.
        /// Викликається після імпорту нових транзакцій.
        /// </summary>
        public async Task AutoMatchPlannedTransactionsAsync(
            Transaction actual,
            CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();

            var searchFrom = actual.Date.AddDays(-3);
            var searchTo = actual.Date.AddDays(3);

            var candidates = context.PlannedTransactions
                .Where(p =>
                    p.AccountId == actual.AccountId &&
                    p.Status == PlannedTransactionStatus.Pending &&
                    p.PlannedDate >= searchFrom &&
                    p.PlannedDate <= searchTo)
                .ToList();

            foreach (var candidate in candidates)
            {
                // Проста евристика: перевіряємо суму та категорію
                var amountMatch = Math.Abs(candidate.Amount - actual.Amount) < 0.01m;
                var categoryMatch = candidate.Category.Equals(actual.Category, StringComparison.OrdinalIgnoreCase);

                if (amountMatch && categoryMatch)
                {
                    await MarkPlannedAsCompletedAsync(candidate.Id, actual.Id, ct);
                    _logger.Information(
                        "Auto-matched planned transaction {PlannedId} with actual {ActualId}",
                        candidate.Id, actual.Id);
                    break; // Одна реальна може поглинути тільки одну планову
                }
            }
        }
    }
}
