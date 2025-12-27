using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class AnalyticsServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DatabaseService _db;
        private readonly AnalyticsService _analytics;

        public AnalyticsServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _db = new DatabaseService(_dbPath);
            _analytics = new AnalyticsService(_db);
            Seed();
        }

        [Fact]
        public void GetTrend_ReturnsMonthlyBalances()
        {
            var trend = _analytics.GetTrend("acc-1", TrendGranularity.Monthly, new DateTime(2025, 1, 1), new DateTime(2025, 3, 31));

            Assert.Equal(3, trend.Count);
            Assert.Equal("2025-01", trend[0].Label);
            Assert.Equal(200m, trend[0].Balance);
        }

        [Fact]
        public void ForecastBalance_UsesLinearRegression()
        {
            var forecast = _analytics.ForecastBalance("acc-1", TrendGranularity.Monthly, periods: 2);

            Assert.Equal(2, forecast.Points.Length);
            Assert.NotEqual(0, forecast.TrendSlope);
        }

        [Fact]
        public void DetectAnomalies_ReturnsOutliers()
        {
            var anomalies = _analytics.DetectAnomalies("acc-1", threshold: 2.5);

            Assert.Contains(anomalies, t => Math.Abs(t.Amount) >= 500);
        }

        [Fact]
        public async Task WarmUpCacheAsync_PopulatesCache()
        {
            await _analytics.WarmUpCacheAsync("acc-1");

            var stats = _analytics.GetAccountStatistics("acc-1");
            Assert.True(stats.TotalTransactions > 0);
        }

        private void Seed()
        {
            var txService = new TransactionService(_db, new DeduplicationService(_db, t => t.TransactionId));
            txService.AddTransaction(new Transaction
            {
                TransactionId = "t1",
                Date = new DateTime(2025, 1, 10),
                Amount = 500,
                Account = "acc-1",
                Category = "Дохід"
            });
            txService.AddTransaction(new Transaction
            {
                TransactionId = "t2",
                Date = new DateTime(2025, 1, 11),
                Amount = -300,
                Account = "acc-1",
                Category = "Витрати"
            });
            txService.AddTransaction(new Transaction
            {
                TransactionId = "t3",
                Date = new DateTime(2025, 2, 5),
                Amount = -600,
                Account = "acc-1",
                Category = "Підписки"
            });
            txService.AddTransaction(new Transaction
            {
                TransactionId = "t4",
                Date = new DateTime(2025, 3, 1),
                Amount = 120,
                Account = "acc-1",
                Category = "Дохід"
            });
        }

        public void Dispose()
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
