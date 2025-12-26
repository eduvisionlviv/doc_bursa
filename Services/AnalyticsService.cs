using System;
using System.Collections.Generic;
using System.Linq;
using FinDesk.Models;
using FinDesk.Services;

namespace FinDesk.Services
{
    /// <summary>
    /// Сервіс для аналітики фінансових даних
    /// </summary>
    public class AnalyticsService
    {
        private readonly DatabaseService _databaseService;

        public AnalyticsService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Отримати статистику по рахунку
        /// </summary>
        public AccountStatistics GetAccountStatistics(string accountNumber, DateTime? startDate = null, DateTime? endDate = null)
        {
            var transactions = _databaseService.GetTransactionsByAccount(accountNumber);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var stats = new AccountStatistics
            {
                AccountNumber = accountNumber,
                TotalTransactions = transactions.Count,
                TotalDebit = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalCredit = transactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                Balance = transactions.Sum(t => t.Amount),
                AverageTransaction = transactions.Any() ? transactions.Average(t => Math.Abs(t.Amount)) : 0,
                LargestDebit = transactions.Where(t => t.Amount > 0).DefaultIfEmpty().Max(t => t?.Amount ?? 0),
                LargestCredit = transactions.Where(t => t.Amount < 0).DefaultIfEmpty().Min(t => t?.Amount ?? 0),
                FirstTransactionDate = transactions.Any() ? transactions.Min(t => t.Date) : (DateTime?)null,
                LastTransactionDate = transactions.Any() ? transactions.Max(t => t.Date) : (DateTime?)null
            };

            return stats;
        }

        /// <summary>
        /// Отримати статистику по групі рахунків
        /// </summary>
        public GroupStatistics GetGroupStatistics(MasterGroup group, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var allTransactions = new List<Transaction>();
            
            foreach (var accountNumber in group.AccountNumbers)
            {
                var transactions = _databaseService.GetTransactionsByAccount(accountNumber);
                allTransactions.AddRange(transactions);
            }

            if (startDate.HasValue)
                allTransactions = allTransactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                allTransactions = allTransactions.Where(t => t.Date <= endDate.Value).ToList();

            var stats = new GroupStatistics
            {
                GroupName = group.Name,
                AccountCount = group.AccountNumbers.Count,
                TotalTransactions = allTransactions.Count,
                TotalDebit = allTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalCredit = allTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                Balance = allTransactions.Sum(t => t.Amount),
                AverageTransaction = allTransactions.Any() ? allTransactions.Average(t => Math.Abs(t.Amount)) : 0
            };

            return stats;
        }

        /// <summary>
        /// Отримати транзакції по категоріях
        /// </summary>
        public Dictionary<string, decimal> GetTransactionsByCategory(string accountNumber, DateTime? startDate = null, DateTime? endDate = null)
        {
            var transactions = _databaseService.GetTransactionsByAccount(accountNumber);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            return transactions
                .GroupBy(t => t.Category ?? "Не визначено")
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));
        }

        /// <summary>
        /// Отримати транзакції по місяцях
        /// </summary>
        public Dictionary<string, MonthlyStatistics> GetMonthlyStatistics(string accountNumber, int year)
        {
            var transactions = _databaseService.GetTransactionsByAccount(accountNumber)
                .Where(t => t.Date.Year == year)
                .ToList();

            var monthlyStats = transactions
                .GroupBy(t => new { t.Date.Year, t.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToDictionary(
                    g => $"{g.Key.Year}-{g.Key.Month:D2}",
                    g => new MonthlyStatistics
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        TotalDebit = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                        TotalCredit = g.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                        TransactionCount = g.Count(),
                        Balance = g.Sum(t => t.Amount)
                    }
                );

            return monthlyStats;
        }

        /// <summary>
        /// Отримати топ контрагентів
        /// </summary>
        public List<CounterpartyStatistics> GetTopCounterparties(string accountNumber, int topCount = 10, DateTime? startDate = null, DateTime? endDate = null)
        {
            var transactions = _databaseService.GetTransactionsByAccount(accountNumber);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var counterpartyStats = transactions
                .Where(t => !string.IsNullOrWhiteSpace(t.Counterparty))
                .GroupBy(t => t.Counterparty)
                .Select(g => new CounterpartyStatistics
                {
                    CounterpartyName = g.Key,
                    TotalAmount = g.Sum(t => Math.Abs(t.Amount)),
                    TransactionCount = g.Count(),
                    AverageAmount = g.Average(t => Math.Abs(t.Amount)),
                    LastTransactionDate = g.Max(t => t.Date)
                })
                .OrderByDescending(c => c.TotalAmount)
                .Take(topCount)
                .ToList();

            return counterpartyStats;
        }

        /// <summary>
        /// Порівняльний аналіз періодів
        /// </summary>
        public PeriodComparison ComparePeriods(string accountNumber, DateTime period1Start, DateTime period1End, DateTime period2Start, DateTime period2End)
        {
            var period1Stats = GetAccountStatistics(accountNumber, period1Start, period1End);
            var period2Stats = GetAccountStatistics(accountNumber, period2Start, period2End);

            return new PeriodComparison
            {
                Period1 = period1Stats,
                Period2 = period2Stats,
                DebitGrowth = CalculateGrowthPercentage(period1Stats.TotalDebit, period2Stats.TotalDebit),
                CreditGrowth = CalculateGrowthPercentage(period1Stats.TotalCredit, period2Stats.TotalCredit),
                TransactionGrowth = CalculateGrowthPercentage(period1Stats.TotalTransactions, period2Stats.TotalTransactions)
            };
        }

        private decimal CalculateGrowthPercentage(decimal oldValue, decimal newValue)
        {
            if (oldValue == 0)
                return newValue > 0 ? 100 : 0;
            
            return ((newValue - oldValue) / oldValue) * 100;
        }
    }

    // Класи для статистики
    public class AccountStatistics
    {
        public string AccountNumber { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }
        public decimal AverageTransaction { get; set; }
        public decimal LargestDebit { get; set; }
        public decimal LargestCredit { get; set; }
        public DateTime? FirstTransactionDate { get; set; }
        public DateTime? LastTransactionDate { get; set; }
    }

    public class GroupStatistics
    {
        public string GroupName { get; set; }
        public int AccountCount { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }
        public decimal AverageTransaction { get; set; }
    }

    public class MonthlyStatistics
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public int TransactionCount { get; set; }
        public decimal Balance { get; set; }
    }

    public class CounterpartyStatistics
    {
        public string CounterpartyName { get; set; }
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageAmount { get; set; }
        public DateTime LastTransactionDate { get; set; }
    }

    public class PeriodComparison
    {
        public AccountStatistics Period1 { get; set; }
        public AccountStatistics Period2 { get; set; }
        public decimal DebitGrowth { get; set; }
        public decimal CreditGrowth { get; set; }
        public decimal TransactionGrowth { get; set; }
    }
}

