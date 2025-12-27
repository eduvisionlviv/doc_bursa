using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models; // Виправлено з FinDesk.Models

namespace doc_bursa.Services // Виправлено з FinDesk.Services
{
    /// <summary>
    /// Керує створенням та виконанням рекурентних транзакцій з простим планувальником.
    /// </summary>
    public class RecurringTransactionService : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly Timer _timer;
        private readonly TimeSpan _pollInterval;
        private bool _isRunning;

        public RecurringTransactionService(DatabaseService databaseService, TransactionService transactionService, TimeSpan? pollInterval = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _pollInterval = pollInterval ?? TimeSpan.FromMinutes(5);
            _timer = new Timer(async _ => await TickAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public IReadOnlyCollection<RecurringTransaction> GetAll(bool onlyActive = false) =>
            _databaseService.GetRecurringTransactions(onlyActive);

        public RecurringTransaction? Get(Guid id) => _databaseService.GetRecurringTransaction(id);

        public RecurringTransaction Create(RecurringTransaction recurring)
        {
            recurring.Id = recurring.Id == Guid.Empty ? Guid.NewGuid() : recurring.Id;
            recurring.CreatedAt = DateTime.UtcNow;
            recurring.UpdatedAt = DateTime.UtcNow;
            recurring.CalculateNextOccurrence(recurring.StartDate);

            _databaseService.SaveRecurringTransaction(recurring);
            return recurring;
        }

        public bool Update(RecurringTransaction recurring)
        {
            if (_databaseService.GetRecurringTransaction(recurring.Id) == null)
            {
                return false;
            }

            recurring.UpdatedAt = DateTime.UtcNow;
            _databaseService.SaveRecurringTransaction(recurring);
            return true;
        }

        public bool Delete(Guid id)
        {
            if (_databaseService.GetRecurringTransaction(id) == null)
            {
                return false;
            }

            _databaseService.DeleteRecurringTransaction(id);
            return true;
        }

        public async Task<int> ProcessDueAsync(DateTime? onDate = null, CancellationToken cancellationToken = default)
        {
            var now = onDate?.Date ?? DateTime.UtcNow.Date;
            var recurringList = _databaseService.GetRecurringTransactions(onlyActive: true);
            var processed = 0;

            foreach (var recurring in recurringList.Where(r => r.IsDue(now)))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var transaction = new Transaction
                {
                    TransactionId = $"rec-{recurring.Id}-{recurring.OccurrenceCount + 1}-{now:yyyyMMdd}",
                    Amount = recurring.Amount,
                    Category = string.IsNullOrWhiteSpace(recurring.Category) ? "Інше" : recurring.Category,
                    Date = now,
                    Description = recurring.Description,
                    Account = recurring.AccountId?.ToString() ?? string.Empty,
                    Source = "Recurring"
                };

                if (_transactionService.AddTransaction(transaction))
                {
                    recurring.MarkAsExecuted(now);
                    _databaseService.SaveRecurringTransaction(recurring);
                    processed++;
                }
            }

            return processed;
        }

        public void Start()
        {
            _isRunning = true;
            _timer.Change(TimeSpan.Zero, _pollInterval);
        }

        public void Stop()
        {
            _isRunning = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        private async Task TickAsync()
        {
            if (!_isRunning)
            {
                return;
            }

            await ProcessDueAsync();
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
