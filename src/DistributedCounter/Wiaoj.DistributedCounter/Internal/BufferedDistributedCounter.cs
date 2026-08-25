using Wiaoj.Concurrency;
using Wiaoj.DistributedCounter.Diagnostics;
using Wiaoj.Primitives;

namespace Wiaoj.DistributedCounter.Internal;

internal sealed class BufferedDistributedCounter : IDistributedCounter {
    private long _expiryTicks = -1;
    public CounterKey Key { get; }
    public CounterStrategy Strategy => CounterStrategy.Buffered;

    /// <summary>
    /// Gets the underlying storage provider assigned to this buffered counter.
    /// </summary>
    public ICounterStorage Storage { get; }

    private readonly AsyncLazy<Empty> _initialSyncTask;
    private long _localDelta;
    private long _baseValue;

    public BufferedDistributedCounter(CounterKey key, ICounterStorage storage) {
        this.Key = key;
        this.Storage = storage;

        this._initialSyncTask = new AsyncLazy<Empty>(async cancellationToken => {
            CounterValue remoteValue = await this.Storage.GetAsync(this.Key, cancellationToken);
            Atomic.Write(ref this._baseValue, remoteValue.Value);
            return Empty.Default;
        });
    }

    private ValueTask<Empty> EnsureInitializedAsync(CancellationToken cancellationToken) {
        return this._initialSyncTask.GetValueAsync(cancellationToken);
    }

    public async ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        await EnsureInitializedAsync(cancellationToken);

        if(expiry.Value.HasValue) {
            Atomic.Exchange(ref this._expiryTicks, expiry.Value.Value.Ticks);
        }

        Atomic.Add(ref this._localDelta, amount);
        DistributedCounterMetrics.RecordIncrement(this.Key.Value, "Buffered", amount);
        long currentTotal = Atomic.Read(ref this._baseValue) + Atomic.Read(ref this._localDelta);
        return new CounterValue(currentTotal);
    }

    public async ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        await FlushAsync(cancellationToken);

        CounterLimitResult result = await this.Storage.TryIncrementAsync(this.Key, amount, limit, expiry, cancellationToken);
        if(result.IsAllowed) {
            Atomic.Write(ref this._baseValue, result.CurrentValue);
        }

        return result;
    }

    public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return IncrementAsync(-amount, expiry, cancellationToken);
    }

    public async ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        await FlushAsync(cancellationToken);

        CounterLimitResult result = await this.Storage.TryDecrementAsync(this.Key, amount, minLimit, expiry, cancellationToken);
        if(result.IsAllowed) {
            Atomic.Write(ref this._baseValue, result.CurrentValue);
        }

        return result;
    }

    public async ValueTask<bool> TryCompareExchangeAsync(
        CounterValue expectedValue,
        CounterValue newValue,
        CounterExpiry expiry,
        CancellationToken cancellationToken) {
        await FlushAsync(cancellationToken);

        bool success = await this.Storage.TryCompareExchangeAsync(
            this.Key,
            expectedValue,
            newValue,
            expiry,
            cancellationToken);

        if(success) {
            Atomic.Write(ref this._baseValue, newValue.Value);
        }

        return success;
    }

    public async ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
        await EnsureInitializedAsync(cancellationToken);
        long val = Atomic.Read(ref this._baseValue) + Atomic.Read(ref this._localDelta);
        return new CounterValue(val);
    }

    public async ValueTask ResetAsync(CancellationToken cancellationToken) {
        Atomic.Exchange(ref this._localDelta, 0);
        Atomic.Exchange(ref this._baseValue, 0);
        await this.Storage.DeleteAsync(this.Key, cancellationToken);
    }

    public async ValueTask SetAsync(long value, CounterExpiry expiry, CancellationToken cancellationToken) {
        Atomic.Exchange(ref this._localDelta, 0);
        Atomic.Write(ref this._baseValue, value);
        if(expiry.Value.HasValue) {
            Atomic.Exchange(ref this._expiryTicks, expiry.Value.Value.Ticks);
        }
        await this.Storage.SetAsync(this.Key, new CounterValue(value), expiry, cancellationToken).ConfigureAwait(false);
    }

    internal bool TryCaptureDelta(out long delta, out CounterExpiry expiry) {
        delta = Atomic.Exchange(ref this._localDelta, 0);
        long ticks = Atomic.Exchange(ref this._expiryTicks, -1);

        expiry = ticks > 0 ? CounterExpiry.FromTicks(ticks) : default;
        return delta != 0 || ticks > 0;
    }

    internal void CommitDelta(long delta) {
        Atomic.Add(ref this._baseValue, delta);
    }

    internal void RollbackDelta(long delta) {
        Atomic.Add(ref this._localDelta, delta);
    }

    internal long SyncWithStorage(long redisRealValue, long justFlushedDelta) {
        long oldBase = Atomic.Read(ref this._baseValue);
        long expectedValue = oldBase + justFlushedDelta;
        Atomic.Write(ref this._baseValue, redisRealValue);
        return redisRealValue - expectedValue;
    }

    internal long GetCurrentBaseValue() {
        return Atomic.Read(ref this._baseValue);
    }

    internal async ValueTask FlushAsync(CancellationToken cancellationToken) {
        await EnsureInitializedAsync(cancellationToken);

        long delta = Atomic.Exchange(ref this._localDelta, 0);
        long ticks = Atomic.Exchange(ref this._expiryTicks, -1);

        if(delta == 0) return;

        CounterExpiry expiryToSend = ticks > 0
            ? CounterExpiry.From(TimeSpan.FromTicks(ticks))
            : default;

        try {
            CounterValue newValue = await this.Storage.AtomicIncrementAsync(
                this.Key,
                delta,
                expiryToSend,
                cancellationToken);

            Atomic.Write(ref this._baseValue, newValue.Value);
        }
        catch {
            Atomic.Add(ref this._localDelta, delta);
            if(ticks > 0) {
                Atomic.Exchange(ref this._expiryTicks, ticks);
            }
            throw;
        }
    }
}