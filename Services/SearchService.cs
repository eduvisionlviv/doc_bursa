using System;
using System.Collections.Generic;
using System.Linq;
using FinDesk.Models;

namespace FinDesk.Services
{
    public class SearchService
    {
        // Пошук транзакцій за текстом
        public IEnumerable<Transaction> SearchByText(IEnumerable<Transaction> transactions, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return transactions;

            searchText = searchText.ToLower();

            return transactions.Where(t =>
                (t.Description?.ToLower().Contains(searchText) ?? false) ||
                (t.Category?.ToLower().Contains(searchText) ?? false) ||
                (t.Account?.ToLower().Contains(searchText) ?? false)
            );
        }

        // Фільтр за категорією
        public IEnumerable<Transaction> FilterByCategory(IEnumerable<Transaction> transactions, string category)
        {
            if (string.IsNullOrWhiteSpace(category))
                return transactions;

            return transactions.Where(t => t.Category != null && t.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        }

        // Фільтр за рахунком
        public IEnumerable<Transaction> FilterByAccount(IEnumerable<Transaction> transactions, string account)
        {
            if (string.IsNullOrWhiteSpace(account))
                return transactions;

            return transactions.Where(t => t.Account != null && t.Account.Equals(account, StringComparison.OrdinalIgnoreCase));
        }

        // Фільтр за датою
        public IEnumerable<Transaction> FilterByDateRange(IEnumerable<Transaction> transactions, DateTime? startDate, DateTime? endDate)
        {
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.TransactionDate >= startDate.Value);

            if (endDate.HasValue)
                transactions = transactions.Where(t => t.TransactionDate <= endDate.Value);

            return transactions;
        }

        // Фільтр за сумою
        public IEnumerable<Transaction> FilterByAmountRange(IEnumerable<Transaction> transactions, decimal? minAmount, decimal? maxAmount)
        {
            if (minAmount.HasValue)
                transactions = transactions.Where(t => t.Amount >= minAmount.Value);

            if (maxAmount.HasValue)
                transactions = transactions.Where(t => t.Amount <= maxAmount.Value);

            return transactions;
        }

        // Фільтр тільки прибутків (позитивні суми)
        public IEnumerable<Transaction> FilterIncomeOnly(IEnumerable<Transaction> transactions)
        {
            return transactions.Where(t => t.Amount > 0);
        }

        // Фільтр тільки витрат (негативні суми)
        public IEnumerable<Transaction> FilterExpensesOnly(IEnumerable<Transaction> transactions)
        {
            return transactions.Where(t => t.Amount < 0);
        }

        // Комбінований пошук з усіма фільтрами
        public IEnumerable<Transaction> SearchWithFilters(
            IEnumerable<Transaction> transactions,
            string searchText = null,
            string category = null,
            string account = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            bool? incomeOnly = null,
            bool? expensesOnly = null)
        {
            var result = transactions;

            if (!string.IsNullOrWhiteSpace(searchText))
                result = SearchByText(result, searchText);

            if (!string.IsNullOrWhiteSpace(category))
                result = FilterByCategory(result, category);

            if (!string.IsNullOrWhiteSpace(account))
                result = FilterByAccount(result, account);

            if (startDate.HasValue || endDate.HasValue)
                result = FilterByDateRange(result, startDate, endDate);

            if (minAmount.HasValue || maxAmount.HasValue)
                result = FilterByAmountRange(result, minAmount, maxAmount);

            if (incomeOnly == true)
                result = FilterIncomeOnly(result);
            else if (expensesOnly == true)
                result = FilterExpensesOnly(result);

            return result;
        }

        // Отримати список унікальних категорій
        public IEnumerable<string> GetUniqueCategories(IEnumerable<Transaction> transactions)
        {
            return transactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Category))
                .Select(t => t.Category)
                .Distinct()
                .OrderBy(c => c);
        }

        // Отримати список унікальних рахунків
        public IEnumerable<string> GetUniqueAccounts(IEnumerable<Transaction> transactions)
        {
            return transactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Account))
                .Select(t => t.Account)
                .Distinct()
                .OrderBy(a => a);
        }
    }
}

