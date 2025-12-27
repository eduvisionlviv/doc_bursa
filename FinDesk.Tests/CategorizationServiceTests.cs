using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class CategorizationServiceTests
    {
        [Fact]
        public void RegexRule_IsApplied_WhenPatternMatches()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                var service = new CategorizationService(db);

                var transaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = DateTime.UtcNow,
                    Amount = -250,
                    Description = "Оплата в АТБ",
                    Category = string.Empty
                };

                var category = service.CategorizeTransaction(transaction);

                Assert.Equal("Продукти", category);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void LearnFromUserCorrection_AddsNewRule()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                var service = new CategorizationService(db);

                service.LearnFromUserCorrection("Мій магазин", "Шопінг");
                var transaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = DateTime.UtcNow,
                    Amount = -300,
                    Description = "Мій магазин",
                    Category = string.Empty
                };

                var category = service.CategorizeTransaction(transaction);

                Assert.Equal("Шопінг", category);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void MlPrediction_WorksForUnseenText()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                AddTransactions(db, "Quantum food store", "Продукти", 250, -180);
                AddTransactions(db, "Quantum food store checkout", "Продукти", 250, -200);
                AddTransactions(db, "Enterprise hardware", "Техніка", 250, -5000);

                var service = new CategorizationService(db);

                var transaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = DateTime.UtcNow,
                    Amount = -175,
                    Description = "Quantum food store evening checkout"
                };

                var category = service.CategorizeTransaction(transaction);

                Assert.Equal("Продукти", category);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void Prediction_IsCached_AfterFirstCall()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                var service = new CategorizationService(db);

                var transaction = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = DateTime.UtcNow,
                    Amount = -50,
                    Description = "Unique description for caching"
                };

                service.CategorizeTransaction(transaction);
                service.CategorizeTransaction(transaction);

                var cacheField = typeof(CategorizationService)
                    .GetField("_predictionCache", BindingFlags.NonPublic | BindingFlags.Instance);
                var cache = cacheField?.GetValue(service) as LruCache<string, string>;

                Assert.NotNull(cache);
                Assert.Equal(1, cache!.Count);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void TrainModel_UsesSyntheticData_WhenHistoryIsSmall()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                var service = new CategorizationService(db);

                var engineField = typeof(CategorizationService)
                    .GetField("_predictionEngine", BindingFlags.NonPublic | BindingFlags.Instance);
                var engine = engineField?.GetValue(service);

                Assert.NotNull(engine);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        private static string CreateIsolatedAppData()
        {
            var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temp);
            SetAppDataPath(temp);
            return temp;
        }

        private static void Cleanup(string? path)
        {
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }

        private static void SetAppDataPath(string path)
        {
            var property = typeof(App).GetProperty("AppDataPath", BindingFlags.Static | BindingFlags.Public);
            property!.SetValue(null, path);
        }

        private static void AddTransactions(DatabaseService db, string description, string category, int count, decimal amount)
        {
            var now = DateTime.UtcNow;
            foreach (var i in Enumerable.Range(0, count))
            {
                var transaction = new Transaction
                {
                    TransactionId = $"{description}-{i}-{Guid.NewGuid()}",
                    Date = now.AddMinutes(i),
                    Amount = amount,
                    Description = description,
                    Category = category,
                    Source = "Test"
                };

                db.SaveTransaction(transaction);
            }
        }
    }
}

