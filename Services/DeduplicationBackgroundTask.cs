using System;
using System.Threading;
using Serilog;

namespace FinDesk.Services
{
    /// <summary>
    /// Фонова задача періодичної перевірки дублікатів.
    /// </summary>
    public class DeduplicationBackgroundTask : IDisposable
    {
        private readonly TransactionService _transactionService;
        private readonly Timer _timer;
        private readonly ILogger _logger;
        private bool _isRunning;

        public DeduplicationBackgroundTask(TransactionService transactionService, TimeSpan? interval = null)
        {
            _transactionService = transactionService;
            _logger = Log.ForContext<DeduplicationBackgroundTask>();
            var dueTime = interval ?? TimeSpan.FromMinutes(30);
            _timer = new Timer(Execute, null, dueTime, dueTime);
        }

        private void Execute(object? state)
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _isRunning = true;
                var marked = _transactionService.BulkDeduplicate();
                if (marked > 0)
                {
                    _logger.Information("Background deduplication marked {Count} duplicates", marked);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Background deduplication failed");
            }
            finally
            {
                _isRunning = false;
            }
        }

        public void Dispose()
        {
            _timer.Dispose();
        }
    }
}
