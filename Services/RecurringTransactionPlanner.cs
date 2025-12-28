using System;
using System.Collections.Generic;
using System.Linq;
using doc_bursa.Models;

namespace doc_bursa.Services
{
    /// <summary>
    /// Генерує планові транзакції на основі рекурентних правил і фактичних операцій.
    /// </summary>
    public static class RecurringTransactionPlanner
    {
        public static List<PlannedTransaction> Generate(IEnumerable<RecurringTransaction> recurringRules, IEnumerable<Transaction> actualTransactions, DateTime from, DateTime to)
        {
            var recurringList = recurringRules.Where(r => r.IsActive).ToList();
            var actualList = actualTransactions.ToList();
            var planned = new List<PlannedTransaction>();

            foreach (var recurring in recurringList)
            {
                foreach (var occurrence in recurring.GetOccurrencesInRange(from, to))
                {
                    var matched = FindMatch(actualList, recurring, occurrence);
                    planned.Add(new PlannedTransaction
                    {
                        RecurringId = recurring.Id,
                        PlannedDate = occurrence.Date,
                        Amount = recurring.Amount,
                        Description = recurring.Description,
                        Category = string.IsNullOrWhiteSpace(recurring.Category) ? "Інше" : recurring.Category,
                        Account = recurring.AccountId?.ToString() ?? string.Empty,
                        IsPlanned = true,
                        LinkedTransactionId = matched?.TransactionId ?? string.Empty
                    });
                }
            }

            return planned
                .OrderBy(p => p.PlannedDate)
                .ThenByDescending(p => p.IsAbsorbed)
                .ToList();
        }

        private static Transaction? FindMatch(IEnumerable<Transaction> actualTransactions, RecurringTransaction recurring, DateTime occurrence)
        {
            return actualTransactions.FirstOrDefault(t =>
                t.Date.Date == occurrence.Date &&
                string.Equals(t.Description, recurring.Description, StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(t.Amount - recurring.Amount) < 0.01m);
        }
    }
}
