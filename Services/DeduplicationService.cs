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
        private const double SimilarityThreshold = 0.82;
        private const double SoftSimilarityThreshold = 0.72;
        private const decimal AmountTolerance = 1.5m;
        private const int DateWindowDays = 2;
        private const int DefaultBatchSize = 500;

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
            var candidates = GetCandidates(transaction).ToList();
            var original = candidates
                .Where(t => IsSameContent(transaction, t))
                .OrderBy(t => t.Date)
                .ThenBy(t => t.Id)
                .FirstOrDefault();

            if (original == null)
            {
                var best = FindMostSimilar(transaction, candidates);
                if (best.Transaction == null || best.Score < SimilarityThreshold)
                {
                    transaction.IsDuplicate = false;
                    transaction.OriginalTransactionId = string.Empty;
                    return false;
                }

                original = best.Transaction;
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
            return BulkDetectAndMark(DefaultBatchSize);
        }

        public int BulkDetectAndMark(int batchSize)
        {
            var all = _databaseService.GetTransactions();
            var totalMarked = 0;

            foreach (var batch in Batch(all, batchSize))
            {
                foreach (var group in GroupDuplicates(batch))
                {
                    var original = group
                        .OrderBy(t => t.Date)
                        .ThenBy(t => t.Id)
                        .First();

                    foreach (var duplicate in group.Where(t => t.TransactionId != original.TransactionId))
                    {
                        if (!duplicate.IsDuplicate || !string.Equals(duplicate.OriginalTransactionId, original.TransactionId, StringComparison.Ordinal))
                        {
                            _databaseService.UpdateDuplicateInfo(duplicate.Id, true, original.TransactionId);
                            totalMarked++;
                        }
                    }
                }
            }

            _logger.Information("Bulk deduplication marked {Count} duplicates with fuzzy grouping", totalMarked);
            return totalMarked;
        }

        private static bool IsSameContent(Transaction left, Transaction right)
        {
            return left.Date == right.Date
                   && left.Amount == right.Amount
                   && string.Equals(left.Description ?? string.Empty, right.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private string BuildBucketKey(Transaction transaction)
        {
            var normalizedDate = transaction.Date.Date.ToString("yyyyMMdd");
            var amountBand = Math.Round(transaction.Amount / 5m, 0);
            return $"{normalizedDate}|{amountBand}";
        }

        private IEnumerable<Transaction> GetCandidates(Transaction transaction)
        {
            var all = _databaseService.GetTransactions();
            return all.Where(t =>
                t.TransactionId != transaction.TransactionId &&
                Math.Abs((transaction.Date - t.Date).TotalDays) <= DateWindowDays &&
                Math.Abs(transaction.Amount - t.Amount) <= Math.Max(AmountTolerance, Math.Abs(transaction.Amount) * 0.05m));
        }

        private (Transaction? Transaction, double Score) FindMostSimilar(Transaction transaction, IEnumerable<Transaction> candidates)
        {
            Transaction? best = null;
            double bestScore = 0;

            foreach (var candidate in candidates)
            {
                var score = ComputeSimilarityScore(transaction, candidate);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return (best, bestScore);
        }

        private double ComputeSimilarityScore(Transaction left, Transaction right)
        {
            var descScore = ComputeDescriptionSimilarity(left.Description ?? string.Empty, right.Description ?? string.Empty);
            var amountScore = ComputeAmountSimilarity(left.Amount, right.Amount);
            var dateScore = ComputeDateScore(left.Date, right.Date);

            return (descScore * 0.6) + (amountScore * 0.25) + (dateScore * 0.15);
        }

        private static double ComputeDescriptionSimilarity(string left, string right)
        {
            left = left.Trim();
            right = right.Trim();

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return 1.0;
            }

            var distance = ComputeLevenshteinDistance(left.ToLowerInvariant(), right.ToLowerInvariant());
            var maxLength = Math.Max(left.Length, right.Length);
            if (maxLength == 0)
            {
                return 1.0;
            }

            return 1.0 - (double)distance / maxLength;
        }

        private static int ComputeLevenshteinDistance(string source, string target)
        {
            if (source == target)
            {
                return 0;
            }

            if (source.Length == 0)
            {
                return target.Length;
            }

            if (target.Length == 0)
            {
                return source.Length;
            }

            var matrix = new int[source.Length + 1, target.Length + 1];
            for (var i = 0; i <= source.Length; i++)
            {
                matrix[i, 0] = i;
            }

            for (var j = 0; j <= target.Length; j++)
            {
                matrix[0, j] = j;
            }

            for (var i = 1; i <= source.Length; i++)
            {
                for (var j = 1; j <= target.Length; j++)
                {
                    var cost = source[i - 1] == target[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[source.Length, target.Length];
        }

        private static double ComputeAmountSimilarity(decimal left, decimal right)
        {
            var max = Math.Max(Math.Abs(left), Math.Abs(right));
            var difference = Math.Abs(left - right);
            var normalized = max == 0 ? (double)difference : (double)difference / (double)max;
            return 1.0 - Math.Min(normalized, 1.0);
        }

        private static double ComputeDateScore(DateTime left, DateTime right)
        {
            var delta = Math.Abs((left - right).TotalDays);
            if (delta < 0.0001)
            {
                return 1.0;
            }

            if (delta <= 1)
            {
                return 0.9;
            }

            if (delta <= DateWindowDays)
            {
                return 0.75;
            }

            return 0.0;
        }

        private IEnumerable<List<Transaction>> GroupDuplicates(IEnumerable<Transaction> batch)
        {
            var result = new List<List<Transaction>>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (var bucket in batch.GroupBy(BuildBucketKey))
            {
                var items = bucket.ToList();
                foreach (var seed in items)
                {
                    if (!visited.Add(seed.TransactionId))
                    {
                        continue;
                    }

                    var cluster = new List<Transaction> { seed };
                    var queue = new Queue<Transaction>();
                    queue.Enqueue(seed);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        foreach (var candidate in items)
                        {
                            if (visited.Contains(candidate.TransactionId) || candidate.TransactionId == current.TransactionId)
                            {
                                continue;
                            }

                            var score = ComputeSimilarityScore(current, candidate);
                            if (score >= SimilarityThreshold || (score >= SoftSimilarityThreshold && IsNearInTime(current, candidate)))
                            {
                                visited.Add(candidate.TransactionId);
                                cluster.Add(candidate);
                                queue.Enqueue(candidate);
                            }
                        }
                    }

                    if (cluster.Count > 1)
                    {
                        result.Add(cluster);
                    }
                }
            }

            return result;
        }

        private static bool IsNearInTime(Transaction left, Transaction right)
        {
            return Math.Abs((left.Date - right.Date).TotalDays) <= DateWindowDays;
        }

        private static IEnumerable<List<T>> Batch<T>(IEnumerable<T> source, int batchSize)
        {
            var batch = new List<T>(batchSize);
            foreach (var item in source)
            {
                batch.Add(item);
                if (batch.Count >= batchSize)
                {
                    yield return new List<T>(batch);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                yield return batch;
            }
        }
    }
}
