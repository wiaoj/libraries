using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Buffers;
using System.Diagnostics;
using Wiaoj.DistributedCounter.Diagnostics;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Internal.Logging;

namespace Wiaoj.DistributedCounter.Hosting;

public class CounterAutoFlushService : BackgroundService {
    private readonly IBufferedCounterSource _source;
    private readonly ICounterStorage _storage;
    private readonly DistributedCounterOptions _options;
    private readonly ILogger<CounterAutoFlushService> _logger;

    public CounterAutoFlushService(
        IDistributedCounterFactory factory,
        ICounterStorage storage,
        IOptions<DistributedCounterOptions> options,
        ILogger<CounterAutoFlushService> logger) {

        this._options = options.Value;
        this._logger = logger;
        this._storage = storage;

        if(factory is IBufferedCounterSource source) {
            this._source = source;
        }
        else {
            throw new InvalidOperationException("Factory IBufferedCounterSource implemente etmiyor. Auto-flush çalışamaz.");
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(this._options.AutoFlushInterval <= TimeSpan.Zero) {
            this._logger.LogWarning("AutoFlushInterval 0 veya negatif. Servis devre dışı.");
            return;
        }

        using PeriodicTimer timer = new(this._options.AutoFlushInterval);

        while(await timer.WaitForNextTickAsync(stoppingToken)) {
            try {
                await FlushBatchAsync(stoppingToken);
            }
            catch(Exception ex) {
                // LogExtensions kullanmıyoruz çünkü generic exception logu standarda uymayabilir, 
                // ama istersen oraya da taşıyabilirsin.
                this._logger.LogError(ex, "Distributed counter Auto-Flush döngüsünde kritik hata.");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        this._logger.LogInformation("Uygulama kapanıyor. Son kez flush yapılıyor...");
        await FlushBatchAsync(cancellationToken);
        await base.StopAsync(cancellationToken);
    }

    private async Task FlushBatchAsync(CancellationToken cancellationToken) {
        // 1. TRACING BAŞLAT (OpenTelemetry)
        using Activity? activity = DistributedCounterTracing.Source.StartActivity("FlushBatch");

        IEnumerable<BufferedDistributedCounter> counters = this._source.GetBufferedCounters();

        int countEstimate = 0;
        if(counters is ICollection<BufferedDistributedCounter> c) countEstimate = c.Count;
        else countEstimate = 128;

        if(countEstimate == 0) return;

        // Tracing Tagleri
        activity?.SetTag("batch.estimate_count", countEstimate);

        // 2. Buffer Kiralama (Zero-Allocation)
        ArrayPool<CounterUpdate> updatesPool = ArrayPool<CounterUpdate>.Shared;
        ArrayPool<(BufferedDistributedCounter Counter, long Delta)> contextPool = ArrayPool<(BufferedDistributedCounter Counter, long Delta)>.Shared;
        ArrayPool<long> resultsPool = ArrayPool<long>.Shared;

        CounterUpdate[] updatesBuffer = updatesPool.Rent(countEstimate);
        (BufferedDistributedCounter, long)[] contextBuffer = contextPool.Rent(countEstimate);
        long[] resultsBuffer = resultsPool.Rent(countEstimate);

        int actualCount = 0;

        try {
            // 3. Veri Toplama
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

            // 4. Redis İşlemi
            long startTimestamp = Stopwatch.GetTimestamp();

            try {
                // Storage'a git
                await this._storage.BatchIncrementAsync(
                    updatesBuffer.AsMemory(0, actualCount),
                    resultsBuffer.AsMemory(0, actualCount),
                    cancellationToken);

                // 5. Commit & Self-Healing & Logging
                for(int i = 0; i < actualCount; i++) {
                    (BufferedDistributedCounter counter, long delta) = contextBuffer[i];
                    long redisVal = resultsBuffer[i]; // Redis'ten gelen yeni değer

                    // a) Local Commit (Eski mantık, base'i artırır)
                    // cancellationTokenx.counter.CommitDelta(ctx.delta); -> ARTIK GEREK YOK çünkü Sync yapacağız.

                    // b) Self-Healing Sync (Drift hesapla ve eşitle)
                    // Not: SyncWithStorage içinde baseValue = redisVal yapılıyor.
                    long drift = counter.SyncWithStorage(redisVal, delta);

                    if(drift != 0) {
                        // Drift varsa logla (Self-Healing Log)
                        long expected = redisVal - drift; // (OldBase + delta)
                        this._logger.LogSelfHealing(counter.Key.Value, expected, redisVal, drift);

                        // Tracing'e event ekle (Hata analizi için)
                        activity?.AddEvent(new ActivityEvent("SelfHealingDrift", tags: new ActivityTagsCollection {
                            { "key", counter.Key.Value },
                            { "drift", drift }
                        }));
                    }
                }

                // Metrics & High-Performance Logging
                DistributedCounterMetrics.RecordFlush();
                TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
                DistributedCounterMetrics.RecordFlushDuration(elapsed.TotalMilliseconds);

                this._logger.LogBatchFlushCompleted(actualCount, elapsed.TotalMilliseconds);
            }
            catch(Exception ex) {
                // 6. Rollback
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