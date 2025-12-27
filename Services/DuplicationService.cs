using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FinDesk.Models;

namespace FinDesk.Services
{
    /// <summary>
    /// Сервіс doc_bursa виявлення та видалення дублікатів транзакцій
    /// Використовує 3 стратегії: Hash, TransactionId, Date+Amount+Description
    /// </doc_bursa>
    public class DuplicationService
    {
        private readonly DatabaseService _db;

        public DuplicationService(DatabaseService databaseService)
        {
            _db = databaseService;
        }

        /// <summary>
        /// Генерує унікальний хеш для транзакції
        /// </summary>
        public string GenerateHash(Transaction transaction)
        {
            var data = $"{transaction.Date:yyyyMMddHHmmss}|{transaction.Amount}|{transaction.Description}|{transaction.Source}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToBase64String(hashBytes);
        }

        /// <summary>
        /// Перевіряє чи транзакція є дублікатом
        /// </summary>
        public bool IsDuplicate(Transaction transaction)
        {
            var existingTransactions = _db.GetTransactions();

            // Стратегія 1: Перевірка по Hash
            if (!string.IsNullOrEmpty(transaction.Hash))
            {
                if (existingTransactions.Any(t => t.Hash == transaction.Hash))
                    return true;
            }

            // Стратегія 2: Перевірка по TransactionId
            if (!string.IsNullOrEmpty(transaction.TransactionId))
            {
                if (existingTransactions.Any(t => t.TransactionId == transaction.TransactionId))
                    return true;
            }

            // Стратегія 3: Перевірка по Date + Amount + Description + Source
            var duplicate = existingTransactions.FirstOrDefault(t =>
                t.Date == transaction.Date &&
                t.Amount == transaction.Amount &&
                t.Description == transaction.Description &&
                t.Source == transaction.Source
            );

            return duplicate != null;
        }

        /// <summary>
        /// Автоматичне очищення дублікатів в БД
        /// </summary>
        public (int removed, List<string> log) RemoveDuplicates()
        {
            var allTransactions = _db.GetTransactions();
            var log = new List<string>();
            var toRemove = new HashSet<int>();

            // Групуємо по Hash
            var hashGroups = allTransactions
                .Where(t => !string.IsNullOrEmpty(t.Hash))
                .GroupBy(t => t.Hash)
                .Where(g => g.Count() > 1);

            foreach (var group in hashGroups)
            {
                var duplicates = group.OrderBy(t => t.Id).Skip(1);
                foreach (var dup in duplicates)
                {
                    toRemove.Add(dup.Id);
                    log.Add($"Hash duplicate: {dup.Date:d} {dup.Amount} {dup.Description}");
                }
            }

            // Групуємо по TransactionId
            var tidGroups = allTransactions
                .Where(t => !string.IsNullOrEmpty(t.TransactionId))
                .GroupBy(t => t.TransactionId)
                .Where(g => g.Count() > 1);

            foreach (var group in tidGroups)
            {
                var duplicates = group.OrderBy(t => t.Id).Skip(1);
                foreach (var dup in duplicates)
                {
                    if (!toRemove.Contains(dup.Id))
                    {
                        toRemove.Add(dup.Id);
                        log.Add($"TransactionId duplicate: {dup.Date:d} {dup.Amount} {dup.Description}");
                    }
                }
            }

            // Групуємо по Date+Amount+Description+Source
            var complexGroups = allTransactions
                .GroupBy(t => new { t.Date, t.Amount, t.Description, t.Source })
                .Where(g => g.Count() > 1);

            foreach (var group in complexGroups)
            {
                var duplicates = group.OrderBy(t => t.Id).Skip(1);
                foreach (var dup in duplicates)
                {
                    if (!toRemove.Contains(dup.Id))
                    {
                        toRemove.Add(dup.Id);
                        log.Add($"Complex duplicate: {dup.Date:d} {dup.Amount} {dup.Description}");
                    }
                }
            }

            // Видаляємо дублікати
            foreach (var id in toRemove)
            {
                _db.DeleteTransaction(id);
            }

            return (toRemove.Count, log);
        }

        /// <summary>
        /// Знаходить потенційні дублікати для перегляду
        /// </summary>
        public List<List<Transaction>> FindPotentialDuplicates()
        {
            var allTransactions = _db.GetTransactions();
            var duplicateGroups = new List<List<Transaction>>();

            // Групи по схожості
            var similarGroups = allTransactions
                .GroupBy(t => new
                {
                    Date = t.Date.Date,
                    Amount = Math.Round(t.Amount, 2),
                    DescriptionPrefix = t.Description?.Substring(0, Math.Min(20, t.Description.Length ?? 0))
                })
                .Where(g => g.Count() > 1)
                .Select(g => g.ToList())
                .ToList();

            return similarGroups;
        }

        /// <summary>
        /// Знаходить кількість дублікатів по рахунку
        /// </summary>
        public Dictionary<string, int> CountDuplicatesBySource()
        {
            var allTransactions = _db.GetTransactions();
            var duplicates = new Dictionary<string, int>();

            var groups = allTransactions
                .GroupBy(t => t.Source)
                .ToList();

            foreach (var group in groups)
            {
                var source = group.Key ?? "Невідоме джерело";
                var transactions = group.ToList();

                var dupCount = transactions
                    .GroupBy(t => new { t.Date, t.Amount, t.Description })
                    .Where(g => g.Count() > 1)
                    .Sum(g => g.Count() - 1);

                if (dupCount > 0)
                {
                    duplicates[source] = dupCount;
                }
            }

            return duplicates;
        }
    }
}


