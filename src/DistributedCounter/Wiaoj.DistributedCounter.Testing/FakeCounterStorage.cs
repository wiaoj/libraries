using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Testing;

/// <summary>
/// A controllable, thread-safe test double for <see cref="ICounterStorage"/> supporting state inspection,
/// time-travel expiration, call tracking, flush history, key overrides, and async flush signaling.
/// </summary>
public sealed class FakeCounterStorage : ICounterStorage {
    private readonly TimeProvider _timeProvider;
    private readonly Dictionary<string, Entry> _data = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CounterValue> _atomicIncrementOverrides = new(StringComparer.Ordinal);
    private readonly List<CounterUpdate> _flushedUpdates = [];

    private Exception? _atomicIncrementException;
    private Exception? _getManyException;
    private CounterValue? _globalAtomicIncrementOverride;

    private int _getCallCount;
    private int _atomicIncrementCallCount;
    private int _deleteCallCount;
    private int _setCallCount;
    private int _batchIncrementCallCount;
    private int _tryIncrementCallCount;
    private int _tryDecrementCallCount;

    private TaskCompletionSource _batchIncrementSignal = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCounterStorage"/> class using the system clock.
    /// </summary>
    public FakeCounterStorage() : this(TimeProvider.System) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeCounterStorage"/> class with a custom <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider instance used for time-travel simulation.</param>
    public FakeCounterStorage(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

    private readonly record struct Entry(long Value, DateTimeOffset? ExpiresAt);

    /// <summary>Gets the total number of <see cref="GetAsync"/> calls.</summary>
    public int GetCallCount => Volatile.Read(ref this._getCallCount);

    /// <summary>Gets the total number of <see cref="AtomicIncrementAsync"/> calls.</summary>
    public int AtomicIncrementCallCount => Volatile.Read(ref this._atomicIncrementCallCount);

    /// <summary>Gets the total number of <see cref="DeleteAsync"/> calls.</summary>
    public int DeleteCallCount => Volatile.Read(ref this._deleteCallCount);

    /// <summary>Gets the total number of <see cref="SetAsync"/> calls.</summary>
    public int SetCallCount => Volatile.Read(ref this._setCallCount);

    /// <summary>Gets the total number of <see cref="BatchIncrementAsync"/> calls triggered by flush operations.</summary>
    public int BatchIncrementCallCount => Volatile.Read(ref this._batchIncrementCallCount); 

    /// <summary>Gets the total number of <see cref="TryIncrementAsync"/> calls.</summary>
    public int TryIncrementCallCount => Volatile.Read(ref this._tryIncrementCallCount);

    /// <summary>Gets the total number of <see cref="TryDecrementAsync"/> calls.</summary>
    public int TryDecrementCallCount => Volatile.Read(ref this._tryDecrementCallCount);

    /// <summary>Gets an immutable snapshot of all counter updates recorded during batch flush operations.</summary>
    public IReadOnlyList<CounterUpdate> FlushedUpdates {
        get {
            lock(this._data) {
                return [.. this._flushedUpdates];
            }
        }
    }

    /// <summary>Gets a snapshot of the active storage key-value pairs.</summary>
    public IReadOnlyDictionary<string, CounterValue> Snapshot {
        get {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            lock(this._data) {
                Dictionary<string, CounterValue> copy = new(StringComparer.Ordinal);
                foreach((string? k, Entry v) in this._data) {
                    if(!v.ExpiresAt.HasValue || v.ExpiresAt.Value > now) {
                        copy[k] = new CounterValue(v.Value);
                    }
                }
                return copy;
            }
        }
    }

    /// <summary>
    /// Waits asynchronously until the next batch increment flush operation is triggered.
    /// </summary>
    /// <param name="timeout">The maximum duration to wait.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    public Task WaitForBatchIncrementAsync(TimeSpan timeout) {
        return WaitForBatchIncrementAsync(timeout, CancellationToken.None);
    }

    /// <summary>
    /// Waits asynchronously until the next batch increment flush operation is triggered, respecting cancellation.
    /// </summary>
    /// <param name="timeout">The maximum duration to wait.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous wait operation.</returns>
    public Task WaitForBatchIncrementAsync(TimeSpan timeout, CancellationToken cancellationToken) {
        return Volatile.Read(ref this._batchIncrementSignal).Task.WaitAsync(timeout, cancellationToken);
    }

    /// <summary>
    /// Resets the batch increment task completion signal for subsequent wait operations.
    /// </summary>
    public void ResetBatchIncrementSignal() {
        Interlocked.Exchange(ref this._batchIncrementSignal, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
    }

    /// <summary>
    /// Pre-seeds a specific counter key with a predetermined value and infinite expiration.
    /// </summary>
    /// <param name="key">The counter key to configure.</param>
    /// <param name="value">The initial counter value.</param>
    public void SetupGetValue(CounterKey key, CounterValue value) {
        lock(this._data) {
            this._data[key.Value] = new Entry(value.Value, null);
        }
    }

    /// <summary>
    /// Configures a global override value to be returned by all subsequent atomic increment operations.
    /// </summary>
    /// <param name="value">The overridden counter value.</param>
    public void SetupAtomicIncrementResult(CounterValue value) {
        this._globalAtomicIncrementOverride = value;
    }

    /// <summary>
    /// Configures an override value to be returned when atomically incrementing a specific key.
    /// </summary>
    /// <param name="key">The counter key to configure.</param>
    /// <param name="value">The overridden counter value.</param>
    public void SetupAtomicIncrementResult(CounterKey key, CounterValue value) {
        lock(this._atomicIncrementOverrides) {
            this._atomicIncrementOverrides[key.Value] = value;
        }
    }

    /// <summary>
    /// Configures subsequent atomic increment operations to throw the specified exception.
    /// </summary>
    /// <param name="ex">The exception to simulate.</param>
    public void SimulateAtomicIncrementFailure(Exception ex) {
        Preca.ThrowIfNull(ex);
        this._atomicIncrementException = ex;
    }

    /// <summary>
    /// Configures subsequent batch get operations to throw the specified exception.
    /// </summary>
    /// <param name="ex">The exception to simulate.</param>
    public void SimulateGetManyFailure(Exception ex) {
        Preca.ThrowIfNull(ex);
        this._getManyException = ex;
    }

    /// <summary>
    /// Resets all internal state, call counters, overrides, flush histories, and simulated exceptions.
    /// </summary>
    public void Reset() {
        lock(this._data) {
            this._data.Clear();
            this._atomicIncrementOverrides.Clear();
            this._flushedUpdates.Clear();
            this._atomicIncrementException = null;
            this._getManyException = null;
            this._globalAtomicIncrementOverride = null;
            Interlocked.Exchange(ref this._getCallCount, 0);
            Interlocked.Exchange(ref this._atomicIncrementCallCount, 0);
            Interlocked.Exchange(ref this._deleteCallCount, 0);
            Interlocked.Exchange(ref this._setCallCount, 0);
            Interlocked.Exchange(ref this._batchIncrementCallCount, 0);
            Interlocked.Exchange(ref this._tryIncrementCallCount, 0);
            Interlocked.Exchange(ref this._tryDecrementCallCount, 0);
        }
        ResetBatchIncrementSignal();
    }

    /// <inheritdoc/>
    public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._getCallCount);
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        lock(this._data) {
            if(this._data.TryGetValue(key.Value, out Entry entry) && (!entry.ExpiresAt.HasValue || entry.ExpiresAt.Value > now)) {
                return new ValueTask<CounterValue>(new CounterValue(entry.Value));
            }
            return new ValueTask<CounterValue>(CounterValue.Zero);
        }
    }

    /// <inheritdoc/>
    public ValueTask<CounterValue> AtomicIncrementAsync(CounterKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._atomicIncrementCallCount);
        if(this._atomicIncrementException is not null) throw this._atomicIncrementException;

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = expiry.Value.HasValue ? now + expiry.Value.Value : null;

        lock(this._data) {
            if(this._atomicIncrementOverrides.TryGetValue(key.Value, out CounterValue keyOverride)) {
                this._data[key.Value] = new Entry(keyOverride.Value, expiresAt);
                return new ValueTask<CounterValue>(keyOverride);
            }

            if(this._globalAtomicIncrementOverride.HasValue) {
                this._data[key.Value] = new Entry(this._globalAtomicIncrementOverride.Value.Value, expiresAt);
                return new ValueTask<CounterValue>(this._globalAtomicIncrementOverride.Value);
            }

            bool exists = this._data.TryGetValue(key.Value, out Entry current);
            bool isExpired = exists && current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now;

            if(!exists || isExpired) {
                this._data[key.Value] = new Entry(amount, expiresAt);
                return new ValueTask<CounterValue>(new CounterValue(amount));
            }

            long next = current.Value + amount;
            this._data[key.Value] = new Entry(next, current.ExpiresAt);
            return new ValueTask<CounterValue>(new CounterValue(next));
        }
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._deleteCallCount);
        lock(this._data) {
            this._data.Remove(key.Value);
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._setCallCount);
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = expiry.Value.HasValue ? now + expiry.Value.Value : null;

        lock(this._data) {
            this._data[key.Value] = new Entry(value.Value, expiresAt);
            return ValueTask.CompletedTask;
        }
    }

    /// <inheritdoc/>
    public ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._tryIncrementCallCount); 
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = expiry.Value.HasValue ? now + expiry.Value.Value : null;

        lock(this._data) {
            bool exists = this._data.TryGetValue(key.Value, out Entry current);
            bool isExpired = exists && current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now;

            long startValue = (!exists || isExpired) ? 0 : current.Value;
            long nextValue = startValue + amount;

            DateTimeOffset? activeExpiry = (!exists || isExpired) ? expiresAt : current.ExpiresAt;
            TimeSpan? liveTtl = activeExpiry.HasValue ? activeExpiry.Value - now : null;

            if(nextValue > limit) {
                TimeSpan? deniedTtl = (exists && !isExpired && current.ExpiresAt.HasValue) ? current.ExpiresAt.Value - now : null;
                return new ValueTask<CounterLimitResult>(new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0, Ttl: deniedTtl));
            }

            this._data[key.Value] = new Entry(nextValue, activeExpiry);
            return new ValueTask<CounterLimitResult>(new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue, Ttl: liveTtl));
        }
    }

    /// <inheritdoc/>
    public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._tryDecrementCallCount);
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = expiry.Value.HasValue ? now + expiry.Value.Value : null;

        lock(this._data) {
            bool exists = this._data.TryGetValue(key.Value, out Entry current);
            bool isExpired = exists && current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now;

            long startValue = (!exists || isExpired) ? 0 : current.Value;
            long nextValue = startValue - amount;

            DateTimeOffset? activeExpiry = (!exists || isExpired) ? expiresAt : current.ExpiresAt;
            TimeSpan? liveTtl = activeExpiry.HasValue ? activeExpiry.Value - now : null;

            if(nextValue < minLimit) {
                TimeSpan? deniedTtl = (exists && !isExpired && current.ExpiresAt.HasValue) ? current.ExpiresAt.Value - now : null;
                return new ValueTask<CounterLimitResult>(new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0, Ttl: deniedTtl));
            }

            this._data[key.Value] = new Entry(nextValue, activeExpiry);
            return new ValueTask<CounterLimitResult>(new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit, Ttl: liveTtl));
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryCompareExchangeAsync(
        CounterKey key,
        CounterValue expectedValue,
        CounterValue newValue,
        CounterExpiry expiry,
        CancellationToken cancellationToken) {

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset? expiresAt = expiry.Value.HasValue ? now + expiry.Value.Value : null;

        lock(this._data) {
            bool exists = this._data.TryGetValue(key.Value, out Entry current);
            bool isExpired = exists && current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now;

            long startValue = (!exists || isExpired) ? 0 : current.Value;

            if(startValue != expectedValue.Value) {
                return new ValueTask<bool>(false);
            }

            this._data[key.Value] = new Entry(newValue.Value, expiresAt);
            return new ValueTask<bool>(true);
        }
    }

    /// <inheritdoc/>
    public ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        lock(this._data) {
            if(this._data.TryGetValue(key.Value, out Entry entry) && entry.ExpiresAt.HasValue && entry.ExpiresAt.Value > now) {
                return new ValueTask<TimeSpan?>(entry.ExpiresAt.Value - now);
            }
            return new ValueTask<TimeSpan?>((TimeSpan?)null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        Dictionary<CounterKey, CounterValue> result = [];
        lock(this._data) {
            foreach(CounterKey key in keys) {
                long val = this._data.TryGetValue(key.Value, out Entry v) && (!v.ExpiresAt.HasValue || v.ExpiresAt.Value > now) ? v.Value : 0;
                result[key] = new CounterValue(val);
            }
        }
        return new ValueTask<IDictionary<CounterKey, CounterValue>>(result);
    }

    /// <inheritdoc/>
    public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) {
        if(this._getManyException is not null) throw this._getManyException;

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        ReadOnlySpan<CounterKey> span = keys.Span;
        Span<CounterValue> dest = destination.Span;
        lock(this._data) {
            for(int i = 0; i < span.Length; i++) {
                long val = this._data.TryGetValue(span[i].Value, out Entry v) && (!v.ExpiresAt.HasValue || v.ExpiresAt.Value > now) ? v.Value : 0;
                dest[i] = new CounterValue(val);
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) {
        Interlocked.Increment(ref this._batchIncrementCallCount);
        ReadOnlySpan<CounterUpdate> span = updates.Span;
        Span<long> dest = resultDestination.Span;
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        lock(this._data) {
            for(int i = 0; i < span.Length; i++) {
                CounterUpdate u = span[i];
                this._flushedUpdates.Add(u);

                DateTimeOffset? expiresAt = u.Expiry.Value.HasValue ? now + u.Expiry.Value.Value : null;

                bool exists = this._data.TryGetValue(u.Key.Value, out Entry current);
                bool isExpired = exists && current.ExpiresAt.HasValue && current.ExpiresAt.Value <= now;

                long cur = (!exists || isExpired) ? 0 : current.Value;
                long nxt = cur + u.Amount;
                DateTimeOffset? activeExp = (!exists || isExpired) ? expiresAt : current.ExpiresAt;

                this._data[u.Key.Value] = new Entry(nxt, activeExp);
                dest[i] = nxt;
            }
        }

        Volatile.Read(ref this._batchIncrementSignal).TrySetResult();
        return ValueTask.CompletedTask;
    }
}