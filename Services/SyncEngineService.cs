using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using doc_bursa.Models;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Оркестратор для циклічного опитування активних джерел даних та імпорту транзакцій.
    /// </summary>
    public class SyncEngineService : IDisposable
    {
        private readonly DatabaseService _databaseService;
        private readonly TransactionService _transactionService;
        private readonly PrivatBankService _privatBankService;
        private readonly MonobankService _monobankService;
        private readonly UkrsibBankService _ukrsibBankService;
        private readonly ILogger _logger;
        private readonly Timer _timer;
        private readonly TimeSpan _pollInterval;
        private readonly CancellationTokenSource _cts = new();
        private bool _isPolling;

        public SyncEngineService(DatabaseService databaseService, TransactionService transactionService, TimeSpan? pollInterval = null)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
            _privatBankService = new PrivatBankService();
            _monobankService = new MonobankService();
            _ukrsibBankService = new UkrsibBankService();
            _logger = Log.ForContext<SyncEngineService>();
            _pollInterval = pollInterval ?? TimeSpan.FromMinutes(15);
            _timer = new Timer(Tick, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, _pollInterval);
            _logger.Information("SyncEngine started with interval {Interval} minutes", _pollInterval.TotalMinutes);
        }

        public void Stop()
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _logger.Information("SyncEngine stopped");
        }

        private void Tick(object? state)
        {
            _ = PollAsync(_cts.Token);
        }

        private async Task PollAsync(CancellationToken cancellationToken)
        {
            if (_isPolling)
            {
                return;
            }

            try
            {
                _isPolling = true;
                var sources = await _databaseService.GetDataSourcesAsync(cancellationToken);
                foreach (var source in sources.Where(s => s.IsEnabled))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SyncSourceAsync(source, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.Warning("SyncEngine polling cancelled");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "SyncEngine polling failed");
            }
            finally
            {
                _isPolling = false;
            }
        }

        private async Task SyncSourceAsync(DataSource source, CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var from = source.LastSync ?? now.AddDays(-7);

            try
            {
                var fetched = await FetchTransactionsAsync(source, from, now, cancellationToken);
                if (fetched.Count > 0)
                {
                    var saved = await _transactionService.ImportTransactionsAsync(fetched, cancellationToken);
                    _logger.Information("Source {SourceName} imported {Saved}/{Total} transactions", source.Name, saved, fetched.Count);
                }
                else
                {
                    _logger.Information("Source {SourceName} returned no transactions", source.Name);
                }

                source.LastSync = now;
                await _databaseService.UpdateDataSourceAsync(source, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync source {SourceName}", source.Name);
            }
        }

        private async Task<List<Transaction>> FetchTransactionsAsync(DataSource source, DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(source.ApiToken) && !source.Type.Equals("CSV Import", StringComparison.OrdinalIgnoreCase))
            {
                _logger.Warning("Source {SourceName} skipped: missing API token", source.Name);
                return new List<Transaction>();
            }

            return (source.Type ?? string.Empty).ToLowerInvariant() switch
            {
                "privatbank" => await _privatBankService.GetTransactionsAsync(source.ApiToken, source.ClientId, from, to),
                "monobank" => await _monobankService.GetTransactionsAsync(source.ApiToken, source.ClientId, from, to),
                "ukrsibbank" => await FetchUkrsibTransactionsAsync(source, from, to, cancellationToken),
                _ => new List<Transaction>()
            };
        }

        private async Task<List<Transaction>> FetchUkrsibTransactionsAsync(DataSource source, DateTime from, DateTime to, CancellationToken cancellationToken)
        {
            var result = await _ukrsibBankService.FetchTransactionsAsync(source.ApiToken, from, to, cancellationToken);
            if (!result.IsSuccess)
            {
                _logger.Warning("Ukrsibbank sync failed for {SourceName}: {Error}", source.Name, result.Error);
            }

            return result.Data ?? new List<Transaction>();
        }

        public void Dispose()
        {
            _cts.Cancel();
            _timer.Dispose();
            _cts.Dispose();
        }
    }
}
