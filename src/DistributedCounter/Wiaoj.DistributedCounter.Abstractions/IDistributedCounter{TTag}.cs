using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// A strongly-typed distributed counter wrapper for dependency injection.
/// </summary>
/// <typeparam name="TTag">The marker type associated with this counter.</typeparam>
public interface IDistributedCounter<TTag> : IDistributedCounter where TTag : notnull {
    IDistributedCounter ForKey<TKey>(TKey key) where TKey : notnull; 
}

public static partial class DistributedCounterExtensions {
    extension<TTag>(IDistributedCounter<TTag> counter) where TTag : notnull {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> GetValueAsync<TKey>(
            TKey key,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).GetValueAsync(cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(
            TKey key,
            long amount,
            CounterExpiry expiry,
            CancellationToken cancellationToken) where TKey : notnull { 
            return counter.ForKey(key).IncrementAsync(amount, expiry, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(
            TKey key,
            long limit,
            long amount = 1,
            CounterExpiry expiry = default,
            CancellationToken cancellationToken = default) where TKey : notnull { 
            return counter.ForKey(key).TryIncrementAsync(amount, limit, expiry, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync<TKey>(
            TKey key,
            long amount = 1,
            CounterExpiry expiry = default,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).DecrementAsync(amount, expiry, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(
            TKey key,
            long minLimit,
            long amount = 1,
            CounterExpiry expiry = default,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryDecrementAsync(amount, minLimit, expiry, cancellationToken);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask ResetAsync<TKey>(
            TKey key,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).ResetAsync(cancellationToken);
        }
    }
}