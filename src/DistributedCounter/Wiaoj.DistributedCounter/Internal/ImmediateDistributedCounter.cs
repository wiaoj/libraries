using Wiaoj.DistributedCounter.Diagnostics;

namespace Wiaoj.DistributedCounter.Internal; 
internal sealed class ImmediateDistributedCounter(CounterKey key, ICounterStorage storage) : IDistributedCounter {
    public CounterKey Key { get; } = key;
    public CounterStrategy Strategy => CounterStrategy.Immediate;

    public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        DistributedCounterMetrics.RecordIncrement(this.Key.Value, "Immediate", amount);
        return storage.AtomicIncrementAsync(this.Key, amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        return storage.TryIncrementAsync(this.Key, amount, limit, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return storage.AtomicIncrementAsync(this.Key, -amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        return storage.TryDecrementAsync(this.Key, amount, minLimit, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
        return storage.GetAsync(this.Key, cancellationToken);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken) {
        return storage.DeleteAsync(this.Key, cancellationToken);
    }
}