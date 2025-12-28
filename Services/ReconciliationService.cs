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
    /// Сервіс для звірки та зв'язування переказів між власними рахунками.
    /// Реалізує модуль "Транзит та Звірка" з документації.
    /// </summary>
    public class ReconciliationService
    {
        private readonly DatabaseService _databaseService;
        private readonly ILogger _logger;

        public ReconciliationService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _logger = Log.ForContext<ReconciliationService>();
        }

        /// <summary>
        /// Отримати всі активні правила звірки.
        /// </summary>
        public async Task<List<ReconciliationRule>> GetActiveRulesAsync(CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            return context.ReconciliationRules
                .Where(r => r.IsActive)
                .ToList();
        }

        /// <summary>
        /// Створити нове правило звірки.
        /// </summary>
        public async Task<ReconciliationRule> CreateRuleAsync(ReconciliationRule rule, CancellationToken ct = default)
        {
            await using var context = _databaseService.CreateDbContext();
            context.ReconciliationRules.Add(rule);
            await context.SaveChangesAsync(ct);
            _logger.Information("Created reconciliation rule: {RuleName}", rule.Name);
            return rule;
        }

        /// <summary>
        /// Спробувати знайти парну транзакцію для переказу.
        /// </summary>
        public async Task<Transaction?> FindMatchingTransferAsync(
            Transaction sourceTransaction,
            ReconciliationRule rule,
            CancellationToken ct = default)
        {
            if (sourceTransaction.Amount >= 0)
            {
                return null; // Шукаємо пару тільки для витрат
            }

            await using var context = _databaseService.CreateDbContext();

            var searchFrom = sourceTransaction.Date.AddDays(-rule.MaxDaysDifference);
            var searchTo = sourceTransaction.Date.AddDays(rule.MaxDaysDifference);
            var expectedAmount = Math.Abs(sourceTransaction.Amount);

            var candidates = context.Transactions
                .Where(t =>
                    t.AccountId == rule.TargetAccountId &&
                    t.Date >= searchFrom &&
                    t.Date <= searchTo &&
                    t.Amount > 0 && // Дохід
                    string.IsNullOrEmpty(t.TransferId)) // Ще не зв'язана
                .ToList();

            foreach (var candidate in candidates)
            {
                var difference = Math.Abs(candidate.Amount - expectedAmount);
                var commissionPercent = (difference / expectedAmount) * 100;

                if (commissionPercent <= rule.MaxCommissionPercent)
                {
                    return candidate;
                }
            }

            return null;
        }

        /// <summary>
        /// Зв'язати дві транзакції як переказ.
        /// </summary>
        public async Task LinkTransferAsync(
            Transaction source,
            Transaction target,
            decimal? commission = null,
            CancellationToken ct = default)
        {
            var transferId = Guid.NewGuid().ToString();

            await using var context = _databaseService.CreateDbContext();

            var sourceDb = context.Transactions.FirstOrDefault(t => t.Id == source.Id);
            var targetDb = context.Transactions.FirstOrDefault(t => t.Id == target.Id);

            if (sourceDb == null || targetDb == null)
            {
                throw new InvalidOperationException("Транзакції не знайдено в базі даних");
            }

            sourceDb.TransferId = transferId;
            sourceDb.Status = TransactionStatus.Completed;
            sourceDb.IsTransfer = true;
            sourceDb.TransferCommission = commission;

            targetDb.TransferId = transferId;
            targetDb.Status = TransactionStatus.Completed;
            targetDb.IsTransfer = true;

            await context.SaveChangesAsync(ct);

            _logger.Information(
                "Linked transfer: {SourceId} -> {TargetId}, TransferId: {TransferId}, Commission: {Commission}",
                source.TransactionId, target.TransactionId, transferId, commission);
        }

        /// <summary>
        /// Автоматична обробка нової транзакції за правилами.
        /// </summary>
        public async Task ProcessTransactionAsync(Transaction transaction, CancellationToken ct = default)
        {
            if (transaction.Amount >= 0 || transaction.IsTransfer)
            {
                return; // Обробляємо тільки витрати, які ще не позначені як переказ
            }

            var rules = await GetActiveRulesAsync(ct);

            foreach (var rule in rules)
            {
                if (transaction.AccountId != rule.SourceAccountId)
                {
                    continue;
                }

                // Перевірка умов
                if (!string.IsNullOrEmpty(rule.CounterpartyPattern) &&
                    !transaction.Counterparty.Contains(rule.CounterpartyPattern, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var match = await FindMatchingTransferAsync(transaction, rule, ct);

                if (match != null)
                {
                    var expectedAmount = Math.Abs(transaction.Amount);
                    var actualAmount = match.Amount;
                    var commission = expectedAmount - actualAmount;

                    await LinkTransferAsync(transaction, match, commission > 0 ? commission : null, ct);
                    _logger.Information(
                        "Auto-matched transfer by rule '{RuleName}': {SourceTx} -> {TargetTx}",
                        rule.Name, transaction.TransactionId, match.TransactionId);
                    return;
                }
                else
                {
                    // Пара не знайдена - встановлюємо статус "В дорозі"
                    await using var context = _databaseService.CreateDbContext();
                    var txDb = context.Transactions.FirstOrDefault(t => t.Id == transaction.Id);
                    if (txDb != null)
                    {
                        txDb.Status = TransactionStatus.InTransit;
                        txDb.IsTransfer = true;
                        await context.SaveChangesAsync(ct);
                        _logger.Information(
                            "Transaction marked as InTransit: {TxId}",
                            transaction.TransactionId);
                    }
                }
            }
        }
    }
}
