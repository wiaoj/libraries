using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// A strongly-typed distributed counter wrapper for dependency injection.
/// </summary>
/// <typeparam name="TTag">The marker type associated with this counter.</typeparam>
public interface IDistributedCounter<TTag> : IDistributedCounter where TTag : notnull {
    /// <summary>
    /// Resolves the concrete <see cref="IDistributedCounter"/> scoped to this tag and the given
    /// identity key (e.g. a user id, an IP address).
    /// </summary>
    /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
    /// <param name="key">The specific identity key to scope the counter to.</param>
    IDistributedCounter ForKey<TKey>(TKey key) where TKey : notnull;
}

/// <summary>
/// Convenience overloads for <see cref="IDistributedCounter{TTag}"/> that collapse the two-step
/// <c>counter.ForKey(key).X(...)</c> call into a single <c>counter.X(key, ...)</c> call. Every
/// member here is a pure delegation — no new behavior — mirroring the same "amount defaults to 1 /
/// expiry defaults to <see cref="CounterExpiry.Infinite"/>" call shapes as the non-generic
/// <see cref="IDistributedCounter"/> extensions, distinguished by parameter type so overrides can
/// be passed positionally instead of by name.
/// </summary>
public static partial class DistributedCounterExtensions {
    extension<TTag>(IDistributedCounter<TTag> counter) where TTag : notnull {

        // --- GetValue ---------------------------------------------------

        /// <summary>Gets the current value of the counter scoped to <paramref name="key"/>.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> GetValueAsync<TKey>(
            TKey key,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).GetValueAsync(cancellationToken);
        }

        // --- Increment ---------------------------------------------------

        /// <summary>Increments the counter scoped to <paramref name="key"/> by 1.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(TKey key) where TKey : notnull {
            return counter.ForKey(key).IncrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>Increments the counter scoped to <paramref name="key"/> by 1 with a cancellation token.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(TKey key, CancellationToken cancellationToken) where TKey : notnull {
            return counter.ForKey(key).IncrementAsync(1, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>Increments the counter scoped to <paramref name="key"/> by 1 with a specific expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(
            TKey key,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).IncrementAsync(1, expiry, cancellationToken);
        }

        /// <summary>Increments the counter scoped to <paramref name="key"/> by the specified amount using default (infinite) expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to increment.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(
            TKey key,
            long amount,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).IncrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Increments the counter scoped to <paramref name="key"/> by the specified amount with a
        /// specific expiry, without requiring an explicit cancellation token.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to increment.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync<TKey>(
            TKey key,
            long amount,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).IncrementAsync(amount, expiry, cancellationToken);
        }

        // --- TryIncrement --------------------------------------------------

        /// <summary>Attempts to increment the counter scoped to <paramref name="key"/> by 1, respecting the specified limit.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="limit">The maximum allowed value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(TKey key, long limit) where TKey : notnull {
            return counter.ForKey(key).TryIncrementAsync(1, limit, CounterExpiry.Infinite, default);
        }

        /// <summary>Attempts to increment the counter scoped to <paramref name="key"/> by 1, respecting the specified limit and expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="limit">The maximum allowed value.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(
            TKey key,
            long limit,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryIncrementAsync(1, limit, expiry, cancellationToken);
        }

        /// <summary>
        /// Attempts to increment the counter scoped to <paramref name="key"/> by the specified
        /// amount (cost), respecting the limit, using default (infinite) expiry.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount (cost) to increment by.</param>
        /// <param name="limit">The maximum allowed value.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(
            TKey key,
            long amount,
            long limit,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryIncrementAsync(amount, limit, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Attempts to increment the counter scoped to <paramref name="key"/> by the specified
        /// amount (cost), respecting the limit and expiry, without requiring an explicit
        /// cancellation token.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount (cost) to increment by.</param>
        /// <param name="limit">The maximum allowed value.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync<TKey>(
            TKey key,
            long amount,
            long limit,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryIncrementAsync(amount, limit, expiry, cancellationToken);
        }

        // --- Decrement ---------------------------------------------------

        /// <summary>Decrements the counter scoped to <paramref name="key"/> by 1.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync<TKey>(TKey key) where TKey : notnull {
            return counter.ForKey(key).DecrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>Decrements the counter scoped to <paramref name="key"/> by the specified amount using default (infinite) expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to decrement.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync<TKey>(
            TKey key,
            long amount,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).DecrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>Decrements the counter scoped to <paramref name="key"/> by 1 with a specific expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync<TKey>(
            TKey key,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).DecrementAsync(1, expiry, cancellationToken);
        }

        /// <summary>
        /// Decrements the counter scoped to <paramref name="key"/> by the specified amount with a
        /// specific expiry, without requiring an explicit cancellation token.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to decrement.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync<TKey>(
            TKey key,
            long amount,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).DecrementAsync(amount, expiry, cancellationToken);
        }

        // --- TryDecrement --------------------------------------------------

        /// <summary>Attempts to decrement the counter scoped to <paramref name="key"/> by 1, respecting the specified minimum limit.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="minLimit">The minimum allowed value.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(TKey key, long minLimit) where TKey : notnull {
            return counter.ForKey(key).TryDecrementAsync(1, minLimit, CounterExpiry.Infinite, default);
        }

        /// <summary>Attempts to decrement the counter scoped to <paramref name="key"/> by 1, respecting the specified minimum limit and expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="minLimit">The minimum allowed value.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(
            TKey key,
            long minLimit,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryDecrementAsync(1, minLimit, expiry, cancellationToken);
        }

        /// <summary>Attempts to decrement the counter scoped to <paramref name="key"/> by the specified amount, respecting the minimum limit, using default (infinite) expiry.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to decrement.</param>
        /// <param name="minLimit">The minimum allowed value.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(
            TKey key,
            long amount,
            long minLimit,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryDecrementAsync(amount, minLimit, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Attempts to decrement the counter scoped to <paramref name="key"/> by the specified
        /// amount, respecting the minimum limit and expiry, without requiring an explicit
        /// cancellation token.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="amount">The amount to decrement.</param>
        /// <param name="minLimit">The minimum allowed value.</param>
        /// <param name="expiry">The expiration policy to apply.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync<TKey>(
            TKey key,
            long amount,
            long minLimit,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryDecrementAsync(amount, minLimit, expiry, cancellationToken);
        }

        // --- TryCompareExchange ------------------------------------------

        /// <summary>
        /// Attempts to atomically replace the counter value scoped to <paramref name="key"/> if matching the expected value, using infinite expiration.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="expectedValue">The value expected to be currently in storage.</param>
        /// <param name="newValue">The new value to set if matching.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> TryCompareExchangeAsync<TKey>(
            TKey key,
            CounterValue expectedValue,
            CounterValue newValue) where TKey : notnull {
            return counter.ForKey(key).TryCompareExchangeAsync(expectedValue, newValue, CounterExpiry.Infinite, default);
        }

        /// <summary>
        /// Attempts to atomically replace the counter value scoped to <paramref name="key"/> if matching the expected value with a cancellation token.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="expectedValue">The value expected to be currently in storage.</param>
        /// <param name="newValue">The new value to set if matching.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> TryCompareExchangeAsync<TKey>(
            TKey key,
            CounterValue expectedValue,
            CounterValue newValue,
            CancellationToken cancellationToken) where TKey : notnull {
            return counter.ForKey(key).TryCompareExchangeAsync(expectedValue, newValue, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Attempts to atomically replace the counter value scoped to <paramref name="key"/> if matching the expected value with specific expiry and cancellation support.
        /// </summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="expectedValue">The value expected to be currently in storage.</param>
        /// <param name="newValue">The new value to set if matching.</param>
        /// <param name="expiry">The expiration policy to apply upon replacement.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> TryCompareExchangeAsync<TKey>(
            TKey key,
            CounterValue expectedValue,
            CounterValue newValue,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).TryCompareExchangeAsync(expectedValue, newValue, expiry, cancellationToken);
        }

        // --- Reset ---------------------------------------------------

        /// <summary>Resets the counter scoped to <paramref name="key"/> to zero and removes it from storage.</summary>
        /// <typeparam name="TKey">The type of the specific identity key.</typeparam>
        /// <param name="key">The specific identity key.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask ResetAsync<TKey>(
            TKey key,
            CancellationToken cancellationToken = default) where TKey : notnull {
            return counter.ForKey(key).ResetAsync(cancellationToken);
        }
    }
}