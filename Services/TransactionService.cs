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
    /// Сервіс роботи з транзакціями з інтегрованою дедуплікацією.
    /// </summary>
    public class TransactionService
    {
        private const decimal SplitTolerance = 0.01m;
        private readonly DatabaseService _databaseService;
        private readonly DeduplicationService _deduplicationService;
        private readonly ILogger _logger = Log.ForContext<TransactionService>();

        public TransactionService(DatabaseService databaseService, DeduplicationService deduplicationService)
        {
            _databaseService = databaseService;
            _deduplicationService = deduplicationService;
        }

        public bool AddTransaction(Transaction transaction)
        {
            // prevent unique constraint failures
            if (_databaseService.GetTransactionByTransactionId(transaction.TransactionId) != null)
            {
                return false;
            }

            _deduplicationService.DetectDuplicate(transaction);
            _databaseService.SaveTransaction(transaction);
            return true;
        }

        public List<Transaction> GetTransactions()
        {
            return _databaseService.GetTransactions();
        }

        public List<Transaction> GetTransactionTree()
        {
            var transactions = _databaseService.GetTransactions();
            return BuildHierarchy(transactions);
        }

        public List<Transaction> GetEffectiveTransactions()
        {
            var all = _databaseService.GetTransactions();
            var roots = BuildHierarchy(all);
            var effective = new List<Transaction>();

            void Collect(Transaction tx)
            {
                if (tx.IsSplit && tx.Children.Any())
                {
                    foreach (var child in tx.Children)
                    {
                        Collect(child);
                    }
                }
                else
                {
                    effective.Add(tx);
                }
            }

            foreach (var root in roots)
            {
                Collect(root);
            }

            return effective;
        }

        public bool MarkAsDuplicate(Guid transactionId)
        {
            return _deduplicationService.MarkAsDuplicate(transactionId);
        }

        public int BulkDeduplicate()
        {
            return _deduplicationService.BulkDetectAndMark();
        }

        public Task<int> AddTransactionsBatchAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var prepared = new List<Transaction>();

                foreach (var transaction in transactions)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    if (_databaseService.GetTransactionByTransactionId(transaction.TransactionId) != null)
                    {
                        continue;
                    }

                    _deduplicationService.DetectDuplicate(transaction);
                    prepared.Add(transaction);
                }

                if (prepared.Count > 0)
                {
                    _databaseService.SaveTransactions(prepared);
                }

                return prepared.Count;
            }, cancellationToken);
        }

        // 👇 ЦЕЙ МЕТОД БУВ ВІДСУТНІЙ, АЛЕ ВИКЛИКАВСЯ В SOURCESVIEWMODEL
        public Task<int> ImportTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
        {
            return AddTransactionsBatchAsync(transactions, cancellationToken);
        }

        public Transaction CreateChildTransaction(Transaction parent, decimal amount, string description, string? category = null, string? account = null)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var child = new Transaction
            {
                TransactionId = $"{parent.TransactionId}-child-{Guid.NewGuid()}",
                ParentTransactionId = parent.TransactionId,
                Date = parent.Date,
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(description) ? parent.Description : description,
                Category = string.IsNullOrWhiteSpace(category) ? parent.Category : category,
                Source = parent.Source,
                Counterparty = parent.Counterparty,
                Account = string.IsNullOrWhiteSpace(account) ? parent.Account : account
            };

            _deduplicationService.DetectDuplicate(child);
            return child;
        }

        public bool ValidateSplitTotals(Transaction parent, IEnumerable<Transaction> children, out decimal difference)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var total = children?.Sum(c => c.Amount) ?? 0;
            difference = Math.Abs(total - parent.Amount);
            return difference <= SplitTolerance;
        }

        public void ApplySplit(Transaction parent, IEnumerable<Transaction> children)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var childList = children?.ToList() ?? new List<Transaction>();
            if (!ValidateSplitTotals(parent, childList, out var diff))
            {
                throw new InvalidOperationException($"Сума дочірніх транзакцій не відповідає батьківській (різниця {diff:N2}).");
            }

            _databaseService.DeleteChildTransactions(parent.TransactionId);
            parent.IsSplit = true;
            _databaseService.SaveTransaction(parent);

            foreach (var child in childList)
            {
                child.ParentTransactionId = parent.TransactionId;
                if (string.IsNullOrWhiteSpace(child.TransactionId))
                {
                    child.TransactionId = $"{parent.TransactionId}-child-{Guid.NewGuid()}";
                }

                _deduplicationService.DetectDuplicate(child);
            }

            if (childList.Count > 0)
            {
                _databaseService.SaveTransactions(childList);
            }

            _logger.Information("Applied split for {TransactionId} into {Count} children", parent.TransactionId, childList.Count);
        }

        private static List<Transaction> BuildHierarchy(List<Transaction> transactions)
        {
            foreach (var tx in transactions)
            {
                tx.Children.Clear();
            }

            var map = transactions.ToDictionary(t => t.TransactionId, StringComparer.OrdinalIgnoreCase);

            foreach (var tx in transactions.Where(t => !string.IsNullOrWhiteSpace(t.ParentTransactionId)))
            {
                if (map.TryGetValue(tx.ParentTransactionId, out var parent))
                {
                    parent.Children.Add(tx);
                }
            }

            return transactions
                .Where(t => string.IsNullOrWhiteSpace(t.ParentTransactionId))
                .OrderByDescending(t => t.Date)
                .ToList();
        }
    }
}
