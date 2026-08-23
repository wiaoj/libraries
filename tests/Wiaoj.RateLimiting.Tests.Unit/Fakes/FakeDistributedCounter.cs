using Wiaoj.DistributedCounter;

namespace Wiaoj.RateLimiting.Tests.Unit.Fakes;

/// <summary>
/// A minimal <see cref="IDistributedCounter"/> that delegates straight to a
/// <see cref="FakeCounterStorage"/> with <see cref="CounterStrategy.Immediate"/> semantics
/// (every call hits storage directly — no local buffering).
/// </summary>
public sealed class FakeDistributedCounter : IDistributedCounter {
    private readonly FakeCounterStorage _storage;

    public FakeDistributedCounter(CounterKey key, FakeCounterStorage storage) {
        this.Key = key;
        this._storage = storage;
    }

    public CounterKey Key { get; }
    public CounterStrategy Strategy => CounterStrategy.Immediate;

    public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._storage.TryIncrementAsync(this.Key, amount, limit, expiry, cancellationToken);
    }

    /// <summary>
    /// Unconditional increment, routed through <see cref="ICounterStorage.AtomicIncrementAsync"/> —
    /// needed by <see cref="SlidingWindowRateLimiter"/>'s speculative-increment-then-rollback pattern.
    /// </summary>
    public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._storage.AtomicIncrementAsync(this.Key, amount, expiry, cancellationToken);
    }

    /// <summary>
    /// Unconditional decrement. Implemented as <c>AtomicIncrementAsync</c> with a negated amount —
    /// mirrors how the real storage backends treat decrement as "increment by a negative number"
    /// (see <c>DistributedCounterRedisLuaScripts</c> / <c>InMemoryCounterStorage</c>).
    /// </summary>
    public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
        return this._storage.AtomicIncrementAsync(this.Key, -amount, expiry, cancellationToken);
    }

    public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeDistributedCounter)} doesn't implement {nameof(TryDecrementAsync)} — no algorithm needs it yet. Extend it if one does.");
    }

    public ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
        return this._storage.GetAsync(this.Key, cancellationToken);
    }

    public ValueTask ResetAsync(CancellationToken cancellationToken) {
        throw new NotSupportedException($"{nameof(FakeDistributedCounter)} doesn't implement {nameof(ResetAsync)} — no algorithm needs it yet. Extend it if one does.");
    }
}