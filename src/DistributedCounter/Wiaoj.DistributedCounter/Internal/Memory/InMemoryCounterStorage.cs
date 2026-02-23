using System.Collections.Concurrent;

namespace Wiaoj.DistributedCounter.Internal.Memory;
/// <summary>
/// Thread-safe, in-memory storage for testing and single-instance applications.
/// Uses Optimistic Concurrency Control (CAS) for atomicity.
/// </summary>
internal sealed class InMemoryCounterStorage : ICounterStorage {

    private readonly ConcurrentDictionary<string, long> _counters = new(StringComparer.Ordinal);

    public ValueTask<CounterValue> AtomicIncrementAsync(CounterKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        //long newValue = this._counters.AddOrUpdate(
        //    key.Value,
        //    addValue: amount,
        //    updateValueFactory: (k, current) => current + amount);

        long newValue = this._counters.AddOrUpdate(
            key: key.Value,
            // Eklerken: Gelen state'i (amount) direkt döndür
            addValueFactory: static (_, amt) => amt,
            // Güncellerken: Mevcut değere, gelen state'i (amount) ekle
            updateValueFactory: static (_, current, amt) => current + amt,
            // State: Dışarıdan içeriye güvenle aktarılacak değer
            factoryArgument: amount);

        return new ValueTask<CounterValue>(newValue);
    }

    public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken) {
        return this._counters.TryGetValue(key.Value, out long val)
            ? new ValueTask<CounterValue>(val)
            : new ValueTask<CounterValue>(CounterValue.Zero);
    }

    public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) {
        this._counters.TryRemove(key.Value, out _);
        return ValueTask.CompletedTask;
    }

    public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) {
        this._counters[key.Value] = value.Value;
        return ValueTask.CompletedTask;
    }

    public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) {
        ReadOnlySpan<CounterUpdate> span = updates.Span;
        Span<long> dest = resultDestination.Span;

        for(int i = 0; i < span.Length; i++) {
            ref readonly CounterUpdate update = ref span[i];
            long amountToAdd = update.Amount;

            long newVal = this._counters.AddOrUpdate(
                update.Key.Value,
                update.Amount,
                (k, current) => current + amountToAdd);

            dest[i] = newVal;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) {
        Dictionary<CounterKey, CounterValue> result = [];
        foreach(CounterKey key in keys) {
            long val = this._counters.TryGetValue(key.Value, out long v) ? v : 0;
            result[key] = new CounterValue(val);
        }
        return new ValueTask<IDictionary<CounterKey, CounterValue>>(result);
    }

    public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) {
        ReadOnlySpan<CounterKey> span = keys.Span;
        Span<CounterValue> dest = destination.Span;

        for(int i = 0; i < span.Length; i++) {
            string keyVal = span[i].Value;
            long val = this._counters.TryGetValue(keyVal, out long v) ? v : 0;
            dest[i] = new CounterValue(val);
        }

        return ValueTask.CompletedTask;
    }

    // Parametre sırası değişti: amount, limit
    public ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        while(true) {
            bool exists = this._counters.TryGetValue(key.Value, out long current);
            long startValue = exists ? current : 0;

            long nextValue = startValue + amount;
            if(nextValue > limit) {
                return new ValueTask<CounterLimitResult>(
                    new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0));
            }

            if(!exists) {
                if(this._counters.TryAdd(key.Value, nextValue)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue));
                }
            }
            else {
                if(this._counters.TryUpdate(key.Value, nextValue, startValue)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: limit - nextValue));
                }
            }
        }
    }

    // Parametre sırası değişti: amount, minLimit
    public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        while(true) {
            bool exists = this._counters.TryGetValue(key.Value, out long current);
            long startValue = exists ? current : 0;

            long nextValue = startValue - amount;
            if(nextValue < minLimit) {
                return new ValueTask<CounterLimitResult>(
                    new CounterLimitResult(IsAllowed: false, CurrentValue: startValue, Remaining: 0));
            }

            if(!exists) {
                if(this._counters.TryAdd(key.Value, nextValue)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit));
                }
            }
            else {
                if(this._counters.TryUpdate(key.Value, nextValue, startValue)) {
                    return new ValueTask<CounterLimitResult>(
                        new CounterLimitResult(IsAllowed: true, CurrentValue: nextValue, Remaining: nextValue - minLimit));
                }
            }
        }
    }
}