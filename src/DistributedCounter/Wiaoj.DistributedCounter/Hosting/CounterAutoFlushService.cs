using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using Wiaoj.DistributedCounter.Diagnostics;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Internal.Logging;

namespace Wiaoj.DistributedCounter.Hosting;

/// <summary>
/// Background service responsible for periodic batch flushing of buffered distributed counters across storages.
/// </summary>
internal sealed class CounterAutoFlushService : BackgroundService {
    private readonly IBufferedCounterSource _source;
    private readonly DistributedCounterOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CounterAutoFlushService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterAutoFlushService"/> class.
    /// </summary>
    public CounterAutoFlushService(
        IDistributedCounterFactory factory,
        IOptions<DistributedCounterOptions> options,
        TimeProvider timeProvider,
        ILogger<CounterAutoFlushService> logger) {

        this._options = options.Value;
        this._timeProvider = timeProvider;
        this._logger = logger;

        if(factory is IBufferedCounterSource source) {
            this._source = source;
        }
        else {
            throw new InvalidOperationException("The configured IDistributedCounterFactory does not implement IBufferedCounterSource. Auto-flush cannot operate.");
        }
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(this._options.AutoFlushInterval <= TimeSpan.Zero) {
            this._logger.LogWarning("AutoFlushInterval is zero or negative. Background flush service is disabled.");
            return;
        }

        using PeriodicTimer timer = new(this._options.AutoFlushInterval, this._timeProvider);

        try {
            while(await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) {
                try {
                    await FlushAllStoragesAsync(stoppingToken).ConfigureAwait(false);
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                    break;
                }
                catch(Exception ex) {
                    this._logger.LogError(ex, "Critical failure occurred during distributed counter auto-flush loop.");
                }
            }
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
    }

    /// <inheritdoc/>
    public override async Task StopAsync(CancellationToken cancellationToken) {
        this._logger.LogInformation("Application is stopping. Performing final distributed counter flush...");
        try {
            await FlushAllStoragesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) {
            this._logger.LogError(ex, "Final batch flush failed during shutdown.");
        }
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task FlushAllStoragesAsync(CancellationToken cancellationToken) {
        IEnumerable<BufferedDistributedCounter> counters = this._source.GetBufferedCounters();

        // Group buffered counters by their assigned storage
        IEnumerable<IGrouping<ICounterStorage, BufferedDistributedCounter>> storageGroups = counters.GroupBy(static c => c.Storage);

        foreach(IGrouping<ICounterStorage, BufferedDistributedCounter> group in storageGroups) {
            await FlushStorageBatchAsync(group.Key, group, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task FlushStorageBatchAsync(
        ICounterStorage storage,
        IEnumerable<BufferedDistributedCounter> counters,
        CancellationToken cancellationToken) {

        using Activity? activity = DistributedCounterTracing.Source.StartActivity("FlushBatch");

        int countEstimate = counters is ICollection<BufferedDistributedCounter> c ? c.Count : 128;
        if(countEstimate == 0) return;

        activity?.SetTag("batch.estimate_count", countEstimate);

        ArrayPool<CounterUpdate> updatesPool = ArrayPool<CounterUpdate>.Shared;
        ArrayPool<(BufferedDistributedCounter Counter, long Delta)> contextPool = ArrayPool<(BufferedDistributedCounter Counter, long Delta)>.Shared;
        ArrayPool<long> resultsPool = ArrayPool<long>.Shared;

        CounterUpdate[] updatesBuffer = updatesPool.Rent(countEstimate);
        (BufferedDistributedCounter Counter, long Delta)[] contextBuffer = contextPool.Rent(countEstimate);
        long[] resultsBuffer = resultsPool.Rent(countEstimate);

        int actualCount = 0;

        try {
            foreach(BufferedDistributedCounter counter in counters) {
                if(counter.TryCaptureDelta(out long delta, out CounterExpiry expiry)) {
                    if(actualCount >= updatesBuffer.Length) {
                        Resize(ref updatesBuffer, updatesPool);
                        Resize(ref contextBuffer, contextPool);
                        Resize(ref resultsBuffer, resultsPool);
                    }

                    updatesBuffer[actualCount] = new CounterUpdate(counter.Key, delta, expiry);
                    contextBuffer[actualCount] = (counter, delta);
                    actualCount++;
                }
            }

            if(actualCount == 0) return;

            activity?.SetTag("batch.actual_count", actualCount);
            long startTimestamp = Stopwatch.GetTimestamp();

            try {
                await storage.BatchIncrementAsync(
                    updatesBuffer.AsMemory(0, actualCount),
                    resultsBuffer.AsMemory(0, actualCount),
                    cancellationToken).ConfigureAwait(false);

                for(int i = 0; i < actualCount; i++) {
                    (BufferedDistributedCounter counter, long delta) = contextBuffer[i];
                    long redisVal = resultsBuffer[i];

                    long drift = counter.SyncWithStorage(redisVal, delta);
                    if(drift != 0) {
                        long expected = redisVal - drift;
                        this._logger.LogSelfHealing(counter.Key.Value, expected, redisVal, drift);

                        activity?.AddEvent(new ActivityEvent("SelfHealingDrift", tags: new ActivityTagsCollection {
                            { "key", counter.Key.Value },
                            { "drift", drift }
                        }));
                    }
                }

                DistributedCounterMetrics.RecordFlush();
                TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                DistributedCounterMetrics.RecordFlushDuration(elapsed.TotalMilliseconds);

                this._logger.LogBatchFlushCompleted(actualCount, elapsed.TotalMilliseconds);
            }
            catch(Exception ex) {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                this._logger.LogFlushFailed(actualCount, ex);

                for(int i = 0; i < actualCount; i++) {
                    (BufferedDistributedCounter counter, long delta) = contextBuffer[i];
                    counter.RollbackDelta(delta);
                }
                throw;
            }
        }
        finally {
            updatesPool.Return(updatesBuffer);
            contextPool.Return(contextBuffer);
            resultsPool.Return(resultsBuffer);
        }
    }

    private static void Resize<T>(ref T[] buffer, ArrayPool<T> pool) {
        int newSize = buffer.Length * 2;
        T[] newBuffer = pool.Rent(newSize);
        Array.Copy(buffer, newBuffer, buffer.Length);
        pool.Return(buffer);
        buffer = newBuffer;
    }
}