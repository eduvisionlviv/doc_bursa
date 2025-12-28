using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using doc_bursa.Services;

namespace doc_bursa.Services
{
    /// <summary>
    /// Сервіс для аналітики фінансових даних
    /// </summary>
    public class AnalyticsService
    {
        private readonly DatabaseService _databaseService;
        private readonly MemoryCache _cache = MemoryCache.Default;
        private readonly CacheItemPolicy _defaultPolicy = new() { SlidingExpiration = TimeSpan.FromMinutes(60) };

        public AnalyticsService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Отримати статистику по рахунку
        /// </summary>
        public AccountStatistics GetAccountStatistics(string accountNumber, DateTime? startDate = null, DateTime? endDate = null, int? masterGroupId = null)
        {
            var cacheKey = $"account-stats:{accountNumber}:{masterGroupId?.ToString() ?? "all"}:{startDate?.ToString("o") ?? "null"}:{endDate?.ToString("o") ?? "null"}";
            if (_cache.Get(cacheKey) is AccountStatistics cachedStats)
            {
                return cachedStats;
            }

            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out var inTransitTransfers);

            var stats = new AccountStatistics
            {
                AccountNumber = accountNumber,
                TotalTransactions = operationalTransactions.Count,
                TotalDebit = operationalTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalCredit = operationalTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                Balance = operationalTransactions.Sum(t => t.Amount),
                AverageTransaction = operationalTransactions.Any() ? operationalTransactions.Average(t => Math.Abs(t.Amount)) : 0,
                LargestDebit = operationalTransactions.Where(t => t.Amount > 0).DefaultIfEmpty().Max(t => t?.Amount ?? 0),
                LargestCredit = operationalTransactions.Where(t => t.Amount < 0).DefaultIfEmpty().Min(t => t?.Amount ?? 0),
                FirstTransactionDate = operationalTransactions.Any() ? operationalTransactions.Min(t => t.Date) : (DateTime?)null,
                LastTransactionDate = operationalTransactions.Any() ? operationalTransactions.Max(t => t.Date) : (DateTime?)null,
                InTransitTransfers = inTransitTransfers
            };

            _cache.Set(cacheKey, stats, _defaultPolicy);
            return stats;
        }

        /// <summary>
        /// Отримати статистику по групі рахунків
        /// </summary>
        public GroupStatistics GetGroupStatistics(MasterGroup group, DateTime? startDate = null, DateTime? endDate = null, int? masterGroupId = null)
        {
            if (group == null)
                throw new ArgumentNullException(nameof(group));

            var cacheKey = $"group-stats:{group.Name}:{masterGroupId?.ToString() ?? "all"}:{startDate?.ToString("o") ?? "null"}:{endDate?.ToString("o") ?? "null"}";
            if (_cache.Get(cacheKey) is GroupStatistics cachedStats)
            {
                return cachedStats;
            }

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

            var filteredTransactions = TransactionFilterHelper.FilterOperationalTransactions(allTransactions, out var inTransitTotal);

            var stats = new GroupStatistics
            {
                GroupName = group.Name,
                AccountCount = group.AccountNumbers.Count,
                TotalTransactions = filteredTransactions.Count,
                TotalDebit = filteredTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalCredit = filteredTransactions.Where(t => t.Amount < 0).Sum(t => Math.Abs(t.Amount)),
                Balance = filteredTransactions.Sum(t => t.Amount),
                AverageTransaction = filteredTransactions.Any() ? filteredTransactions.Average(t => Math.Abs(t.Amount)) : 0,
                InTransitTransfers = inTransitTotal
            };

            _cache.Set(cacheKey, stats, _defaultPolicy);
            return stats;
        }

        /// <summary>
        /// Отримати транзакції по категоріях
        /// </summary>
        public Dictionary<string, decimal> GetTransactionsByCategory(string accountNumber, DateTime? startDate = null, DateTime? endDate = null, int? masterGroupId = null)
        {
            var cacheKey = $"cat:{accountNumber}:{masterGroupId?.ToString() ?? "all"}:{startDate?.ToString("o") ?? "null"}:{endDate?.ToString("o") ?? "null"}";
            if (_cache.Get(cacheKey) is Dictionary<string, decimal> cachedCategories)
            {
                return cachedCategories;
            }

            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out _);

            var result = operationalTransactions
                .GroupBy(t => t.Category ?? "Не визначено")
                .ToDictionary(g => g.Key, g => g.Sum(t => Math.Abs(t.Amount)));

            _cache.Set(cacheKey, result, _defaultPolicy);
            return result;
        }

        public Dictionary<string, decimal> GetCategoryBreakdown(IEnumerable<Transaction> transactions)
        {
            if (transactions == null) throw new ArgumentNullException(nameof(transactions));

            return transactions
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Category) ? "Не визначено" : t.Category)
                .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        }

        /// <summary>
        /// Отримати транзакції по місяцях
        /// </summary>
        public Dictionary<string, MonthlyStatistics> GetMonthlyStatistics(string accountNumber, int year, int? masterGroupId = null)
        {
            var cacheKey = $"monthly:{accountNumber}:{year}:{masterGroupId?.ToString() ?? "all"}";
            if (_cache.Get(cacheKey) is Dictionary<string, MonthlyStatistics> cachedMonthly)
            {
                return cachedMonthly;
            }

            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId)
                .Where(t => t.Date.Year == year)
                .ToList();
            transactions = FilterTransfers(transactions);

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out _);

            var monthlyStats = operationalTransactions
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

            _cache.Set(cacheKey, monthlyStats, _defaultPolicy);
            return monthlyStats;
        }

        public List<TrendPoint> GetTrend(string accountNumber, TrendGranularity granularity, DateTime? from = null, DateTime? to = null, int? masterGroupId = null)
        {
            var cacheKey = $"trend:{accountNumber}:{granularity}:{masterGroupId?.ToString() ?? "all"}:{from?.ToString("o") ?? "null"}:{to?.ToString("o") ?? "null"}";
            if (_cache.Get(cacheKey) is List<TrendPoint> cached)
            {
                return cached;
            }

            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId);
            if (from.HasValue)
                transactions = transactions.Where(t => t.Date >= from.Value).ToList();

            if (to.HasValue)
                transactions = transactions.Where(t => t.Date <= to.Value).ToList();

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out _);

            var grouped = granularity switch
            {
                TrendGranularity.Daily => operationalTransactions.GroupBy(t => t.Date.Date),
                TrendGranularity.Weekly => operationalTransactions.GroupBy(t => FirstDayOfWeek(t.Date)),
                _ => operationalTransactions.GroupBy(t => new DateTime(t.Date.Year, t.Date.Month, 1))
            };

            var trend = grouped
                .OrderBy(g => g.Key)
                .Select(g => new TrendPoint
                {
                    Label = granularity switch
                    {
                        TrendGranularity.Daily => g.Key.ToString("yyyy-MM-dd"),
                        TrendGranularity.Weekly => $"{g.Key:yyyy-MM-dd}",
                        _ => $"{g.Key:yyyy-MM}"
                    },
                    Income = g.Where(t => t.Amount > 0).Sum(t => t.Amount),
                    Expenses = Math.Abs(g.Where(t => t.Amount < 0).Sum(t => t.Amount)),
                    Balance = g.Sum(t => t.Amount)
                })
                .ToList();

            _cache.Set(cacheKey, trend, _defaultPolicy);
            return trend;
        }

        public ForecastResult ForecastBalance(string accountNumber, TrendGranularity granularity, int periods = 3, DateTime? from = null, DateTime? to = null, int? masterGroupId = null)
        {
            var trend = GetTrend(accountNumber, granularity, from, to, masterGroupId);
            if (trend.Count < 2)
            {
                return new ForecastResult { Points = Array.Empty<ForecastPoint>() };
            }

            var xs = Enumerable.Range(0, trend.Count).Select(i => (double)i).ToArray();
            var ys = trend.Select(t => (double)t.Balance).ToArray();

            var (slope, intercept) = LinearRegression(xs, ys);
            var forecastPoints = new List<ForecastPoint>();

            for (int i = 1; i <= periods; i++)
            {
                var index = trend.Count - 1 + i;
                forecastPoints.Add(new ForecastPoint
                {
                    Index = index,
                    PredictedBalance = intercept + slope * index
                });
            }

            return new ForecastResult
            {
                Points = forecastPoints.ToArray(),
                TrendSlope = slope
            };
        }

        public List<Transaction> DetectAnomalies(string accountNumber, double threshold = 3.0, DateTime? from = null, DateTime? to = null, int? masterGroupId = null)
        {
            var cacheKey = $"anomalies:{accountNumber}:{threshold}:{masterGroupId?.ToString() ?? "all"}:{from?.ToString("o") ?? "null"}:{to?.ToString("o") ?? "null"}";
            if (_cache.Get(cacheKey) is List<Transaction> cached)
            {
                return cached;
            }

            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId);
            if (from.HasValue)
                transactions = transactions.Where(t => t.Date >= from.Value).ToList();

            if (to.HasValue)
                transactions = transactions.Where(t => t.Date <= to.Value).ToList();

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out _);

            var amounts = operationalTransactions.Select(t => Math.Abs(t.Amount)).ToArray();
            if (amounts.Length == 0)
            {
                return new List<Transaction>();
            }

            var mean = amounts.Average(); // Returns decimal
            var variance = amounts.Select(a => Math.Pow((double)a - (double)mean, 2)).Average(); // Виправлено: явне приведення mean до double
            var stdDev = Math.Sqrt(variance);

            if (stdDev == 0)
            {
                return new List<Transaction>();
            }

            var anomalies = operationalTransactions.Where(t =>
            {
                var z = (Math.Abs(t.Amount) - mean) / (decimal)stdDev;
                return Math.Abs(z) >= (decimal)threshold;
            }).ToList();

            _cache.Set(cacheKey, anomalies, _defaultPolicy);
            return anomalies;
        }

        private static List<Transaction> FilterTransfers(List<Transaction> transactions, bool includeTransfers = false)
        {
            return includeTransfers ? transactions : transactions.Where(t => !t.IsTransfer).ToList();
        }

        public async Task WarmUpCacheAsync(string accountNumber, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                _ = GetAccountStatistics(accountNumber);
                _ = GetMonthlyStatistics(accountNumber, DateTime.UtcNow.Year);
                _ = GetTrend(accountNumber, TrendGranularity.Monthly);
            }, cancellationToken);
        }

        /// <summary>
        /// Отримати топ контрагентів
        /// </summary>
        public List<CounterpartyStatistics> GetTopCounterparties(string accountNumber, int topCount = 10, DateTime? startDate = null, DateTime? endDate = null, int? masterGroupId = null)
        {
            var transactions = _databaseService.GetTransactionsByAccount(accountNumber, masterGroupId);
            
            if (startDate.HasValue)
                transactions = transactions.Where(t => t.Date >= startDate.Value).ToList();
            
            if (endDate.HasValue)
                transactions = transactions.Where(t => t.Date <= endDate.Value).ToList();

            var operationalTransactions = TransactionFilterHelper.FilterOperationalTransactions(transactions, out _);

            var counterpartyStats = operationalTransactions
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
        public PeriodComparison ComparePeriods(string accountNumber, DateTime period1Start, DateTime period1End, DateTime period2Start, DateTime period2End, int? masterGroupId = null)
        {
            var period1Stats = GetAccountStatistics(accountNumber, period1Start, period1End, masterGroupId);
            var period2Stats = GetAccountStatistics(accountNumber, period2Start, period2End, masterGroupId);

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

        private static DateTime FirstDayOfWeek(DateTime dateTime)
        {
            var diff = (7 + (dateTime.DayOfWeek - DayOfWeek.Monday)) % 7;
            return dateTime.Date.AddDays(-1 * diff);
        }

        private static (double Slope, double Intercept) LinearRegression(IReadOnlyList<double> xs, IReadOnlyList<double> ys)
        {
            var n = xs.Count;
            var sumX = xs.Sum();
            var sumY = ys.Sum();
            var sumXY = xs.Zip(ys, (x, y) => x * y).Sum();
            var sumX2 = xs.Sum(x => x * x);

            var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - Math.Pow(sumX, 2));
            var intercept = (sumY - slope * sumX) / n;
            return (slope, intercept);
        }
    }

    // Класи для статистики
    public class AccountStatistics
    {
        public string AccountNumber { get; set; } = string.Empty;
        public int TotalTransactions { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }
        public decimal AverageTransaction { get; set; }
        public decimal LargestDebit { get; set; }
        public decimal LargestCredit { get; set; }
        public DateTime? FirstTransactionDate { get; set; }
        public DateTime? LastTransactionDate { get; set; }
        public decimal InTransitTransfers { get; set; }
    }

    public class GroupStatistics
    {
        public string GroupName { get; set; } = string.Empty;
        public int AccountCount { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal Balance { get; set; }
        public decimal AverageTransaction { get; set; }
        public decimal InTransitTransfers { get; set; }
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
        public string CounterpartyName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int TransactionCount { get; set; }
        public decimal AverageAmount { get; set; }
        public DateTime LastTransactionDate { get; set; }
    }

    public class PeriodComparison
    {
        public AccountStatistics Period1 { get; set; } = new();
        public AccountStatistics Period2 { get; set; } = new();
        public decimal DebitGrowth { get; set; }
        public decimal CreditGrowth { get; set; }
        public decimal TransactionGrowth { get; set; }
    }

    public enum TrendGranularity
    {
        Daily,
        Weekly,
        Monthly
    }

    public class TrendPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expenses { get; set; }
        public decimal Balance { get; set; }
    }

    public class ForecastPoint
    {
        public int Index { get; set; }
        public double PredictedBalance { get; set; }
    }

    public class ForecastResult
    {
        public ForecastPoint[] Points { get; set; } = Array.Empty<ForecastPoint>();
        public double TrendSlope { get; set; }
    }
}
