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
        private readonly CategorizationService _categorizationService;
        private readonly ILogger _logger;

        public TransactionService(
            DatabaseService databaseService,
            DeduplicationService deduplicationService,
            CategorizationService categorizationService)
        {
            _databaseService = databaseService;
            _deduplicationService = deduplicationService;
            _categorizationService = categorizationService;
            _logger = Log.ForContext<TransactionService>();
        }

        public bool AddTransaction(Transaction transaction)
        {
            var prepared = PrepareTransaction(transaction);

            if (_databaseService.GetTransactionByTransactionId(prepared.TransactionId) != null)
            {
                return false;
            }

            _deduplicationService.DetectDuplicate(prepared);
            _databaseService.SaveTransaction(prepared);
            _logger.Information("Transaction added: {TransactionId} ({Source})", prepared.TransactionId, prepared.Source);
            return true;
        }

        public List<Transaction> GetTransactions(DateTime? from = null, DateTime? to = null, string? category = null, string? account = null, int? masterGroupId = null)
        {
            return _databaseService.GetTransactions(from, to, category, account, masterGroupId);
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

                    var normalized = PrepareTransaction(transaction);
                    if (_databaseService.GetTransactionByTransactionId(normalized.TransactionId) != null)
                    {
                        continue;
                    }

                    _deduplicationService.DetectDuplicate(normalized);
                    prepared.Add(normalized);
                }

                if (prepared.Count > 0)
                {
                    _databaseService.SaveTransactions(prepared);
                    _logger.Information("Batch import saved {Count} transactions", prepared.Count);
                }

                return prepared.Count;
            }, cancellationToken);
        }

        // 👇 ЦЕЙ МЕТОД БУВ ВІДСУТНІЙ, АЛЕ ВИКЛИКАВСЯ В SOURCESVIEWMODEL
        public Task<int> ImportTransactionsAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
        {
            return AddTransactionsBatchAsync(transactions, cancellationToken);
        }

        private Transaction PrepareTransaction(Transaction transaction)
        {
            NormalizationHelper.NormalizeTransaction(transaction);
            if (string.IsNullOrWhiteSpace(transaction.Category))
            {
                transaction.Category = _categorizationService.CategorizeTransaction(transaction);
            }

            return transaction;
        }

                // Методи для Split (заглушки для компіляції)
        public void ValidateSplitTotals(Transaction parent, IEnumerable<Transaction> children)
        {
            if (children == null) return;
            decimal total = children.Sum(x => x.Amount);
            if (Math.Abs(parent.Amount - total) > 0.01m)
            {
                throw new InvalidOperationException("Сума частин не збігається з сумою транзакції.");
            }
        }

            public async Task ApplySplit(Transaction parent, List<Transaction> children)
    {
        // 1. ВАЛІДАЦІЯ (Бізнес-правило: Сума частин = Сумі цілого)
        // Використовуємо Math.Abs для коректної роботи і з витратами (-), і з доходами (+)
        decimal parentAmount = Math.Abs(parent.Amount);
        decimal childrenTotal = children.Sum(c => Math.Abs(c.Amount));

        if (Math.Abs(parentAmount - childrenTotal) > 0.01m)
        {
            throw new InvalidOperationException($"Помилка балансу: Сума частин ({childrenTotal}) не збігається з транзакцією ({parentAmount}).");
        }

        // 2. ПІДГОТОВКА ДАНИХ
        // Маркуємо батьківську транзакцію, щоб вона не враховувалась у звітах двічі
        parent.IsSplit = true; 
        
        // Налаштовуємо дочірні транзакції
        foreach (var child in children)
        {
            // Критично: генеруємо новий ID, бо це внутрішні сутності FinDesk, банк про них не знає
            child.TransactionId = Guid.NewGuid().ToString(); 
            
            // Зв'язок
            child.ParentTransactionId = parent.TransactionId;
            
            // Спадкування метаданих
            child.Date = parent.Date;
            child.AccountId = parent.AccountId;
            child.Account = parent.Account;
            child.Currency = parent.Currency;
            child.Source = "Split"; // Маркер, що це штучний запис
            child.IsSplit = false;  // Дитина не може бути розділена (поки що)
        }

        // 3. АТОМАРНЕ ЗБЕРЕЖЕННЯ (Transaction Scope)
        var batch = new List<Transaction> { parent };
        batch.AddRange(children);

        // Використовуємо існуючий метод, який огортає все в SQL Transaction
        await _databaseService.SaveTransactionsAsync(batch);
        
        _logger.Information("Transaction {Tid} split into {Count} parts", parent.TransactionId, children.Count);
    }

        public Transaction CreateChildTransaction(Transaction parent)
        {
            return new Transaction
            {
                Date = parent.Date,
                AccountId = parent.AccountId,
                Currency = parent.Currency,
                // Копіюємо інші потрібні поля
            };
        }
    }
}
