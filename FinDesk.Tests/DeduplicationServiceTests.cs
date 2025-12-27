using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class DeduplicationServiceTests
    {
        [Fact]
        public void ExactDuplicates_AreDetectedOnInsert()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var (db, txService) = CreateServices();
                var date = DateTime.UtcNow;

                var first = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = date,
                    Amount = 100m,
                    Description = "Subscription"
                };

                var second = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = date,
                    Amount = 100m,
                    Description = "Subscription"
                };

                Assert.True(txService.AddTransaction(first));
                Assert.True(txService.AddTransaction(second));

                var saved = db.GetTransactionByTransactionId(second.TransactionId);
                Assert.NotNull(saved);
                Assert.True(saved!.IsDuplicate);
                Assert.Equal(first.TransactionId, saved.OriginalTransactionId);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void SimilarButDifferent_NotMarkedAsDuplicate()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var (db, txService) = CreateServices();
                var date = DateTime.UtcNow;

                var first = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = date,
                    Amount = 100m,
                    Description = "Coffee shop"
                };

                var second = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = date,
                    Amount = 101m,
                    Description = "Coffee shop"
                };

                Assert.True(txService.AddTransaction(first));
                Assert.True(txService.AddTransaction(second));

                var saved = db.GetTransactionByTransactionId(second.TransactionId);
                Assert.NotNull(saved);
                Assert.False(saved!.IsDuplicate);
                Assert.True(string.IsNullOrEmpty(saved.OriginalTransactionId));
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void BulkDeduplication_MarksExistingRecords()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var (db, txService, dedup) = CreateServices(withDeduplicationInstance: true);
                var date = DateTime.UtcNow.Date;

                foreach (var i in Enumerable.Range(0, 3))
                {
                    var transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        Date = date,
                        Amount = 50m,
                        Description = "Transit fare",
                        IsDuplicate = false
                    };
                    dedup.EnsureHash(transaction);
                    db.SaveTransaction(transaction);
                }

                var marked = dedup.BulkDetectAndMark();
                var all = db.GetTransactions();

                Assert.Equal(2, marked);
                Assert.Equal(2, all.Count(t => t.IsDuplicate));
                Assert.Equal(2, all.Select(t => t.OriginalTransactionId).Count(s => !string.IsNullOrEmpty(s)));
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void HashCollisions_DoNotProduceFalsePositives()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var db = new DatabaseService();
                var dedup = new DeduplicationService(db, _ => "collision");
                var txService = new TransactionService(db, dedup);

                var first = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = new DateTime(2024, 1, 1),
                    Amount = 10m,
                    Description = "A"
                };

                var second = new Transaction
                {
                    TransactionId = Guid.NewGuid().ToString(),
                    Date = new DateTime(2024, 1, 2),
                    Amount = 20m,
                    Description = "B"
                };

                Assert.True(txService.AddTransaction(first));
                Assert.True(txService.AddTransaction(second));

                var savedSecond = db.GetTransactionByTransactionId(second.TransactionId);
                Assert.NotNull(savedSecond);
                Assert.False(savedSecond!.IsDuplicate);
            }
            finally
            {
                Cleanup(temp);
            }
        }

        [Fact]
        public void HandlesThousandTransactions_Performantly()
        {
            var temp = CreateIsolatedAppData();
            try
            {
                var (db, txService) = CreateServices();
                var sw = Stopwatch.StartNew();

                foreach (var i in Enumerable.Range(0, 1000))
                {
                    var transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        Date = DateTime.UtcNow.AddMinutes(i),
                        Amount = i,
                        Description = $"Payment {i}"
                    };
                    Assert.True(txService.AddTransaction(transaction));
                }

                sw.Stop();
                var all = db.GetTransactions();

                Assert.Equal(1000, all.Count);
                Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Elapsed: {sw.Elapsed}");
            }
            finally
            {
                Cleanup(temp);
            }
        }

        private static (DatabaseService db, TransactionService service) CreateServices()
        {
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var tx = new TransactionService(db, dedup);
            return (db, tx);
        }

        private static (DatabaseService db, TransactionService service, DeduplicationService dedup) CreateServices(bool withDeduplicationInstance)
        {
            var db = new DatabaseService();
            var dedup = new DeduplicationService(db);
            var tx = new TransactionService(db, dedup);
            return (db, tx, dedup);
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
    }
}

