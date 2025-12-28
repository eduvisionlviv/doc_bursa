using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Polly;
using Serilog;

namespace doc_bursa.Services
{
    /// <summary>
    /// Локальна черга синхронізації із повторними спробами (Polly) і відстеженням стану мережі.
    /// </summary>
    public class SyncQueueService : IDisposable
    {
        private readonly Channel<SyncJob> _channel;
        private readonly CancellationTokenSource _cts = new();
        private readonly ILogger _logger;
        private readonly AsyncPolicy _retryPolicy;
        private readonly Task _processorTask;

        private record SyncJob(string Name, Func<CancellationToken, Task> Work, TaskCompletionSource Completion);

        public event Action<bool, string>? NetworkStatusChanged;
        public event Action<string>? StatusChanged;

        public SyncQueueService(ILogger? logger = null)
        {
            _channel = Channel.CreateUnbounded<SyncJob>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });
            _logger = logger ?? Log.ForContext<SyncQueueService>();

            _retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    onRetry: (ex, delay, attempt, _) =>
                    {
                        _logger.Warning(ex, "Retry {Attempt} after {Delay} for sync job", attempt, delay);
                        StatusChanged?.Invoke($"Повторна спроба {attempt}: {ex.Message}");
                    });

            _processorTask = Task.Run(ProcessQueueAsync);
        }

        public Task EnqueueAsync(string name, Func<CancellationToken, Task> work)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var job = new SyncJob(name, work, tcs);
            if (!_channel.Writer.TryWrite(job))
            {
                tcs.SetException(new InvalidOperationException("Не вдалося поставити задачу у чергу."));
            }

            StatusChanged?.Invoke($"Додано в чергу: {name}");
            return tcs.Task;
        }

        private async Task ProcessQueueAsync()
        {
            try
            {
                await foreach (var job in _channel.Reader.ReadAllAsync(_cts.Token))
                {
                    var isOnline = NetworkInterface.GetIsNetworkAvailable();
                    var networkMessage = isOnline ? "Мережа доступна" : "Немає мережі - очікуємо";
                    _logger.Information("Network state before {Job}: {Message}", job.Name, networkMessage);
                    NetworkStatusChanged?.Invoke(isOnline, networkMessage);

                    if (!isOnline)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), _cts.Token);
                    }

                    try
                    {
                        StatusChanged?.Invoke($"Виконання: {job.Name}");
                        await _retryPolicy.ExecuteAsync(ct => job.Work(ct), _cts.Token);
                        job.Completion.SetResult();
                        StatusChanged?.Invoke($"Завершено: {job.Name}");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Sync job {Job} failed", job.Name);
                        job.Completion.SetException(ex);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }

        public void Dispose()
        {
            _channel.Writer.TryComplete();
            _cts.Cancel();
            try
            {
                _processorTask.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // ignore
            }
            _cts.Dispose();
        }
    }
}
