using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Утиліти для фільтрації транзакцій з урахуванням транзиту та планових операцій.
    /// </summary>
    public static class TransactionFilterHelper
    {
        private static readonly string[] TransitKeywords = { "транзит", "transit", "transfer", "переказ" };
        private static readonly string[] PlannedKeywords = { "planned", "заплан", "планов", "scheduled" };
        private static readonly string[] PendingKeywords = { "pending", "очікує", "в дорозі", "in transit" };

        public static List<Transaction> FilterOperationalTransactions(IEnumerable<Transaction> transactions, out decimal inTransitTotal)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));

            var operational = new List<Transaction>();
            decimal pendingTransfers = 0;

            foreach (var transaction in transactions)
            {
                if (IsPendingTransfer(transaction))
                {
                    pendingTransfers += Math.Abs(transaction.Amount);
                    continue;
                }

                if (IsTransit(transaction) || IsPlanned(transaction))
                {
                    continue;
                }

                operational.Add(transaction);
            }

            inTransitTotal = pendingTransfers;
            return operational;
        }

        public static bool IsTransit(Transaction transaction)
        {
            return ContainsKeyword(transaction.Category, TransitKeywords)
                   || ContainsKeyword(transaction.Description, TransitKeywords)
                   || ContainsKeyword(transaction.Source, TransitKeywords);
        }

        public static bool IsPlanned(Transaction transaction)
        {
            return transaction.Date > DateTime.Now
                   || ContainsKeyword(transaction.Description, PlannedKeywords)
                   || ContainsKeyword(transaction.Source, PlannedKeywords);
        }

        public static bool IsPendingTransfer(Transaction transaction)
        {
            return IsTransit(transaction) && (ContainsKeyword(transaction.Description, PendingKeywords)
                                              || ContainsKeyword(transaction.Source, PendingKeywords));
        }

        /// <summary>
        /// Отримати ефективні транзакції з урахуванням контексту MasterGroup.
        /// Перекази між рахунками однієї групи не враховуються. Для глобального перегляду всі внутрішні перекази ігноруються.
        /// </summary>
        public static List<Transaction> GetEffectiveTransactions(
            IEnumerable<Transaction> allTransactions,
            HashSet<string>? contextAccountNumbers = null)
        {
            if (allTransactions == null) throw new ArgumentNullException(nameof(allTransactions));

            var result = new List<Transaction>();

            // Глобальний контекст: усі внутрішні перекази не впливають на суму, тому їх ігноруємо.
            if (contextAccountNumbers == null || contextAccountNumbers.Count == 0)
            {
                foreach (var tx in allTransactions)
                {
                    if (!tx.IsTransfer)
                    {
                        result.Add(tx);
                    }
                }

                return result;
            }

            // Контекст конкретної MasterGroup.
            var accountsInContext = contextAccountNumbers;

            foreach (var tx in allTransactions)
            {
                // 1) Якщо це звичайна транзакція (не переказ) -> беремо завжди.
                if (!tx.IsTransfer)
                {
                    result.Add(tx);
                    continue;
                }

                // 2) Для переказів у межах обраної MasterGroup показуємо тільки ті, де рахунок належить групі.
                var account = tx.Account ?? string.Empty;
                if (accountsInContext.Contains(account))
                {
                    result.Add(tx);
                }
            }

            return result;
        }

        private static bool ContainsKeyword(string? source, IReadOnlyCollection<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            return keywords.Any(keyword => source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);
        }
    }
}
