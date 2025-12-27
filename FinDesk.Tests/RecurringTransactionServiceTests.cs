using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FinDesk.Models;
using FinDesk.Services;
using Xunit;

namespace FinDesk.Tests
{
    public class RecurringTransactionServiceTests : IDisposable
    {
        private readonly string _dbPath;
        private readonly DatabaseService _db;
        private readonly DeduplicationService _dedup;
        private readonly TransactionService _txService;
        private readonly RecurringTransactionService _recurringService;

        public RecurringTransactionServiceTests()
        {
            _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
            _db = new DatabaseService(_dbPath);
            _dedup = new DeduplicationService(_db, t => t.TransactionId);
            _txService = new TransactionService(_db, _dedup);
            _recurringService = new RecurringTransactionService(_db, _txService, TimeSpan.FromMilliseconds(50));
        }

        [Fact]
        public async Task ProcessDueAsync_AddsTransactionsAndReschedules()
        {
            var recurring = new RecurringTransaction
            {
                Description = "Щомісячна підписка",
                Amount = -199m,
                Category = "Підписки",
                Frequency = RecurrenceFrequency.Monthly,
                StartDate = new DateTime(2025, 1, 1),
                NextOccurrence = new DateTime(2025, 2, 1)
            };

            _recurringService.Create(recurring);

            var processed = await _recurringService.ProcessDueAsync(new DateTime(2025, 2, 1));

            Assert.Equal(1, processed);
            var stored = _db.GetTransactions();
            Assert.Single(stored);
            Assert.Equal(-199m, stored.First().Amount);
            var savedRecurring = _db.GetRecurringTransaction(recurring.Id);
            Assert.NotNull(savedRecurring);
            Assert.True(savedRecurring!.OccurrenceCount >= 1);
            Assert.True(savedRecurring.NextOccurrence > new DateTime(2025, 2, 1));
        }

        [Fact]
        public void Delete_RemovesRecurring()
        {
            var recurring = _recurringService.Create(new RecurringTransaction
            {
                Description = "Щоденний запис",
                Amount = 10m,
                Frequency = RecurrenceFrequency.Daily
            });

            var removed = _recurringService.Delete(recurring.Id);

            Assert.True(removed);
            Assert.Null(_recurringService.Get(recurring.Id));
        }

        public void Dispose()
        {
            _recurringService.Dispose();
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
    }
}
