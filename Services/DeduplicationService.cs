using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinDesk.Models;
using Serilog;

namespace FinDesk.Services
{
    /// <summary>
    /// Сервіс хеш-базової дедуплікації транзакцій.
    /// Використовує SHA256(Date+Amount+Description) для визначення дублікатів.
    /// </summary>
    public class DeduplicationService
    {
        private readonly DatabaseService _databaseService;
        private readonly Func<Transaction, string> _hashProvider;
        private readonly ILogger _logger;

        public DeduplicationService(DatabaseService databaseService, Func<Transaction, string>? hashProvider = null)
        {
            _databaseService = databaseService;
            _hashProvider = hashProvider ?? ComputeHash;
            _logger = Log.ForContext<DeduplicationService>();
        }

        public string ComputeHash(Transaction transaction)
        {
            var payload = $"{transaction.Date:O}|{transaction.Amount}|{transaction.Description}".Trim();
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(payload);
            var hashBytes = sha.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Генерує хеш та оновлює транзакцію.
        /// </summary>
        public void EnsureHash(Transaction transaction)
        {
            transaction.Hash = _hashProvider(transaction);
        }

        /// <summary>
        /// Перевіряє нову транзакцію на дублікат та заповнює службові поля.
        /// </summary>
        public bool DetectDuplicate(Transaction transaction)
        {
            EnsureHash(transaction);
            var all = _databaseService.GetTransactions();
            var original = all
                .Where(t => IsSameContent(transaction, t))
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .FirstOrDefault();

            if (original == null)
            {
                transaction.IsDuplicate = false;
                transaction.OriginalTransactionId = string.Empty;
                return false;
            }

            transaction.IsDuplicate = !string.Equals(transaction.TransactionId, original.TransactionId, StringComparison.Ordinal);
            transaction.OriginalTransactionId = original.TransactionId;
            return transaction.IsDuplicate;
        }

        /// <summary>
        /// Позначити транзакцію як дублікат існуючої за її TransactionId (GUID).
        /// </summary>
        public bool MarkAsDuplicate(Guid transactionId)
        {
            var transaction = _databaseService.GetTransactionByTransactionId(transactionId.ToString());
            if (transaction == null)
            {
                return false;
            }

            EnsureHash(transaction);
            var all = _databaseService.GetTransactions();
            var original = all
                .Where(t => t.TransactionId != transaction.TransactionId && IsSameContent(transaction, t))
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .FirstOrDefault();

            if (original == null)
            {
                return false;
            }

            _databaseService.UpdateDuplicateInfo(transaction.Id, true, original.TransactionId);
            return true;
        }

        /// <summary>
        /// Пакетний пошук дублікатів для існуючих записів.
        /// </summary>
        public int BulkDetectAndMark()
        {
            var all = _databaseService.GetTransactions();
            var totalMarked = 0;

            foreach (var group in all.GroupBy(BuildGroupKey))
            {
                var originals = group
                    .OrderBy(t => t.IsDuplicate)
                    .ThenBy(t => t.Date)
                    .ThenBy(t => t.Id)
                    .ToList();

                var original = originals.First();
                foreach (var duplicate in originals.Skip(1))
                {
                    if (!duplicate.IsDuplicate || duplicate.OriginalTransactionId != original.TransactionId)
                    {
                        _databaseService.UpdateDuplicateInfo(duplicate.Id, true, original.TransactionId);
                        totalMarked++;
                    }
                }
            }

            _logger.Information("Bulk deduplication marked {Count} duplicates", totalMarked);
            return totalMarked;
        }

        private static bool IsSameContent(Transaction left, Transaction right)
        {
            return left.Date == right.Date
                   && left.Amount == right.Amount
                   && string.Equals(left.Description ?? string.Empty, right.Description ?? string.Empty, StringComparison.Ordinal);
        }

        private string BuildGroupKey(Transaction transaction)
        {
            var normalized = $"{transaction.Date:O}|{transaction.Amount}|{transaction.Description}".Trim();
            return $"{_hashProvider(transaction)}::{normalized}";
        }
    }
}
