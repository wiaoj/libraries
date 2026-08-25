using System.Collections.Concurrent;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Internal.Memory;

/// <summary>
/// Thread-safe in-memory storage for testing and single-instance applications with sliding window expiration.
/// </summary>
internal sealed class InMemoryCounterStorage : ICounterStorage {
    private readonly ConcurrentDictionary<string, CounterEntry> _counters = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCounterStorage"/> class using the system clock.
    /// </summary>
    public InMemoryCounterStorage() : this(TimeProvider.System) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCounterStorage"/> class with a custom <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider used to calculate time-to-live expiration.</param>
    public InMemoryCounterStorage(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

    // ExpiresAt uses DateTimeOffset.MaxValue as the "never expires" sentinel (mirrors CounterExpiry.Infinite),
    // rather than a nullable DateTimeOffset — keeps the struct comparable without null-branching on every read.
    private readonly record struct CounterEntry(long Value, DateTimeOffset ExpiresAt);

    /// <summary>
    /// Converts an entry's <c>ExpiresAt</c> sentinel into the nullable TTL shape <see cref="CounterLimitResult.Ttl"/>
    /// expects — <see langword="null"/> for <see cref="DateTimeOffset.MaxValue"/> (never expires), mirroring
    /// Redis's <c>PTTL</c> semantics where a key with no expiry reports "no TTL" rather than a duration.
    /// </summary>
    private static TimeSpan? ToTtl(DateTimeOffset expiresAt, DateTimeOffset now) {
        return expiresAt == DateTimeOffset.MaxValue ? null : expiresAt - now;
    }

    /// <inheritdoc/>
    public ValueTask<CounterValue> AtomicIncrementAsync(CounterKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = expiry.Value.HasValue ? now.Add(expiry.Value.Value) : DateTimeOffset.MaxValue;

        CounterEntry entry = this._counters.AddOrUpdate(
            key.Value,
            static (_, state) => new CounterEntry(state.amount, state.expiresAt),
            static (_, current, state) => current.ExpiresAt <= state.now
                ? new CounterEntry(state.amount, state.expiresAt)
                : new CounterEntry(current.Value + state.amount, current.ExpiresAt),
            (amount, expiresAt, now));

        return new ValueTask<CounterValue>(entry.Value);
    }

    /// <inheritdoc/>
    public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        if(this._counters.TryGetValue(key.Value, out CounterEntry entry) && entry.ExpiresAt > now) {
            return new ValueTask<CounterValue>(entry.Value);
        }
        return new ValueTask<CounterValue>(CounterValue.Zero);
    }

    /// <summary>
    /// Retrieves the remaining time-to-live for a counter key.
    /// </summary>
    /// <param name="key">The counter key to inspect.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>
    /// The remaining TTL, or <see langword="null"/> when the key does not exist, has already
    /// expired, or was stored with <see cref="CounterExpiry.Infinite"/> (mirrors Redis's
    /// <c>PTTL</c>/<c>KeyTimeToLiveAsync</c> semantics for a key with no expiration).
    /// </returns>
    public ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        if(!this._counters.TryGetValue(key.Value, out CounterEntry entry) || entry.ExpiresAt <= now) {
            return new ValueTask<TimeSpan?>((TimeSpan?)null);
        }

        return new ValueTask<TimeSpan?>(ToTtl(entry.ExpiresAt, now));
    }

    /// <inheritdoc/>
    public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) {
        this._counters.TryRemove(key.Value, out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = expiry.Value.HasValue ? now.Add(expiry.Value.Value) : DateTimeOffset.MaxValue;
        this._counters[key.Value] = new CounterEntry(value.Value, expiresAt);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) {
        ReadOnlySpan<CounterUpdate> span = updates.Span;
        Span<long> dest = resultDestination.Span;
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        for(int i = 0; i < span.Length; i++) {
            CounterUpdate update = span[i];
            long amount = update.Amount;
            DateTimeOffset expiresAt = update.Expiry.Value.HasValue ? now.Add(update.Expiry.Value.Value) : DateTimeOffset.MaxValue;

            CounterEntry entry = this._counters.AddOrUpdate(
                update.Key.Value,
                static (_, state) => new CounterEntry(state.amount, state.expiresAt),
                static (_, current, state) => current.ExpiresAt <= state.now
                    ? new CounterEntry(state.amount, state.expiresAt)
                    : new CounterEntry(current.Value + state.amount, current.ExpiresAt),
                (amount, expiresAt, now));

            dest[i] = entry.Value;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) {
        Dictionary<CounterKey, CounterValue> result = [];
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        foreach(CounterKey key in keys) {
            long val = this._counters.TryGetValue(key.Value, out CounterEntry v) && v.ExpiresAt > now ? v.Value : 0;
            result[key] = new CounterValue(val);
        }
        return new ValueTask<IDictionary<CounterKey, CounterValue>>(result);
    }

    /// <inheritdoc/>
    public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) {
        ReadOnlySpan<CounterKey> span = keys.Span;
        Span<CounterValue> dest = destination.Span;
        DateTimeOffset now = this._timeProvider.GetUtcNow();

        for(int i = 0; i < span.Length; i++) {
            string keyVal = span[i].Value;
            long val = this._counters.TryGetValue(keyVal, out CounterEntry v) && v.ExpiresAt > now ? v.Value : 0;
            dest[i] = new CounterValue(val);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = expiry.Value.HasValue ? now.Add(expiry.Value.Value) : DateTimeOffset.MaxValue;

        while(!cancellationToken.IsCancellationRequested) {
            bool exists = this._counters.TryGetValue(key.Value, out CounterEntry current);

            bool isExpired = exists && current.ExpiresAt <= now;
            long startValue = (!exists || isExpired) ? 0 : current.Value;

            long nextValue = startValue + amount;
            if(nextValue > limit) {
                // Denied — report the TTL of whatever window is currently live (none, if the key
                // never existed or already expired: nothing to wait out yet).
                TimeSpan? deniedTtl = (exists && !isExpired) ? ToTtl(current.ExpiresAt, now) : null;
                return new ValueTask<CounterLimitResult>(
                    new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0, Ttl: deniedTtl));
            }

            CounterEntry nextEntry = new(nextValue, (!exists || isExpired) ? expiresAt : current.ExpiresAt);

            if(!exists || isExpired) {
                if(isExpired) {
                    if(this._counters.TryUpdate(key.Value, nextEntry, current)) {
                        return new ValueTask<CounterLimitResult>(
                            new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                    }
                }
                else if(this._counters.TryAdd(key.Value, nextEntry)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                }
            }
            else {
                if(this._counters.TryUpdate(key.Value, nextEntry, current)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                }
            }
        }

        return ValueTask.FromCanceled<CounterLimitResult>(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = expiry.Value.HasValue ? now.Add(expiry.Value.Value) : DateTimeOffset.MaxValue;

        while(!cancellationToken.IsCancellationRequested) {
            bool exists = this._counters.TryGetValue(key.Value, out CounterEntry current);
            bool isExpired = exists && current.ExpiresAt <= now;
            long startValue = (!exists || isExpired) ? 0 : current.Value;

            long nextValue = startValue - amount;
            if(nextValue < minLimit) {
                TimeSpan? deniedTtl = (exists && !isExpired) ? ToTtl(current.ExpiresAt, now) : null;
                return new ValueTask<CounterLimitResult>(
                    new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0, Ttl: deniedTtl));
            }

            CounterEntry nextEntry = new(nextValue, (!exists || isExpired) ? expiresAt : current.ExpiresAt);

            if(!exists || isExpired) {
                if(isExpired) {
                    if(this._counters.TryUpdate(key.Value, nextEntry, current)) {
                        return new ValueTask<CounterLimitResult>(
                            new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                    }
                }
                else if(this._counters.TryAdd(key.Value, nextEntry)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                }
            }
            else {
                if(this._counters.TryUpdate(key.Value, nextEntry, current)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit, Ttl: ToTtl(nextEntry.ExpiresAt, now)));
                }
            }
        }

        return ValueTask.FromCanceled<CounterLimitResult>(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<bool> TryCompareExchangeAsync(
        CounterKey key,
        CounterValue expectedValue,
        CounterValue newValue,
        CounterExpiry expiry,
        CancellationToken cancellationToken) {

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = expiry.Value.HasValue ? now.Add(expiry.Value.Value) : DateTimeOffset.MaxValue;

        while(!cancellationToken.IsCancellationRequested) {
            bool exists = this._counters.TryGetValue(key.Value, out CounterEntry current);
            bool isExpired = exists && current.ExpiresAt <= now;
            long startValue = (!exists || isExpired) ? 0 : current.Value;

            if(startValue != expectedValue.Value) {
                return new ValueTask<bool>(false);
            }

            CounterEntry newEntry = new(newValue.Value, expiresAt);

            if(!exists || isExpired) {
                if(isExpired) {
                    if(this._counters.TryUpdate(key.Value, newEntry, current)) {
                        return new ValueTask<bool>(true);
                    }
                }
                else if(this._counters.TryAdd(key.Value, newEntry)) {
                    return new ValueTask<bool>(true);
                }
            }
            else {
                if(this._counters.TryUpdate(key.Value, newEntry, current)) {
                    return new ValueTask<bool>(true);
                }
            }
        }

        return ValueTask.FromCanceled<bool>(cancellationToken);
    }
}