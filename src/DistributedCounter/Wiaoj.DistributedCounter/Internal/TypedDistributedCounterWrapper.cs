namespace Wiaoj.DistributedCounter.Internal;

internal class TypedDistributedCounterWrapper<TTag>(IDistributedCounterFactory factory)
    : IDistributedCounter<TTag> where TTag : notnull {

    private readonly Lazy<IDistributedCounter> _inner = new(factory.Create<TTag>);

    public CounterKey Key => this._inner.Value.Key;
    public CounterStrategy Strategy => this._inner.Value.Strategy;

    public IDistributedCounter ForKey<TKey>(TKey key) where TKey : notnull {
        return factory.Create<TTag, TKey>(key);
    }

    public ValueTask<CounterValue> IncrementAsync<TKey>(TKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) where TKey : notnull {
        return ForKey(key).IncrementAsync(amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(TKey key, long limit, long amount, CounterExpiry expiry , CancellationToken cancellationToken) where TKey : notnull {
        return ForKey(key).TryIncrementAsync(amount, limit, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> DecrementAsync<TKey>(TKey key, long amount, CounterExpiry expiry = default, CancellationToken cancellationToken = default) where TKey : notnull {
        return ForKey(key).DecrementAsync(amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(TKey key, long minLimit, long amount,  CounterExpiry expiry, CancellationToken cancellationToken = default) where TKey : notnull {
        return ForKey(key).TryDecrementAsync(amount, minLimit, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> GetValueAsync<TKey>(TKey key, CancellationToken cancellationToken) where TKey : notnull {
        return ForKey(key).GetValueAsync(cancellationToken);
    }

    public ValueTask ResetAsync<TKey>(TKey key, CancellationToken cancellationToken) where TKey : notnull {
        return ForKey(key).ResetAsync(cancellationToken);
    }

    // --- Global (TTag) metotlar ---
    public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._inner.Value.IncrementAsync(amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._inner.Value.TryIncrementAsync(amount, limit, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._inner.Value.DecrementAsync(amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._inner.Value.TryDecrementAsync(minLimit, amount, expiry, cancellationToken);
    }

    public ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
        return this._inner.Value.GetValueAsync(cancellationToken);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken) {
        return this._inner.Value.ResetAsync(cancellationToken);
    }
}