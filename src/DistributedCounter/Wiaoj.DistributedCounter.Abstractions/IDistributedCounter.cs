using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// Represents a high-level distributed counter instance.
/// Defines the core contract without default values to simplify implementation.
/// </summary>
public interface IDistributedCounter {
    /// <summary>
    /// Gets the unique key of the counter.
    /// </summary>
    CounterKey Key { get; }

    /// <summary>
    /// Gets the strategy used for synchronizing this counter.
    /// </summary>
    CounterStrategy Strategy { get; }

    /// <summary>
    /// Increments the counter by the specified amount.
    /// </summary>
    /// <param name="amount">The amount to increment.</param>
    /// <param name="expiry">The expiration policy to apply.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to increment the counter only if the new value stays within the provided limit.
    /// </summary>
    /// <param name="amount">The amount to increment.</param>
    /// <param name="limit">The maximum allowed value.</param>
    /// <param name="expiry">The expiration policy to apply.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken);

    /// <summary>
    /// Decrements the counter by the specified amount.
    /// </summary>
    /// <param name="amount">The amount to decrement.</param>
    /// <param name="expiry">The expiration policy to apply.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to decrement the counter only if the new value is greater than or equal to the minimum limit.
    /// </summary>
    /// <param name="amount">The amount to decrement.</param>
    /// <param name="minLimit">The minimum allowed value.</param>
    /// <param name="expiry">The expiration policy to apply.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the current value of the counter. For buffered strategies, this may return a locally cached estimate.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resets the counter to zero and removes it from storage.
    /// </summary>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    ValueTask ResetAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Provides convenient extension methods for <see cref="IDistributedCounter"/>.
/// </summary>
public static partial class DistributedCounterExtensions {
    extension(IDistributedCounter counter) {
        /// <summary>
        /// Increments the counter by 1.
        /// </summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync() {
            return counter.IncrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>
        /// Increments the counter by 1 with a cancellation token.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(CancellationToken cancellationToken) {
            return counter.IncrementAsync(1, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Increments the counter by 1 with a specific expiry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.IncrementAsync(1, expiry, cancellationToken);
        }

        /// <summary>
        /// Increments the counter by the specified amount using default expiry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(long amount, CancellationToken cancellationToken = default) {
            return counter.IncrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Attempts to increment the counter by 1, respecting the specified limit.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long limit) {
            return counter.TryIncrementAsync(1, limit, CounterExpiry.Infinite, default);
        }

        /// <summary>
        /// Attempts to increment the counter by 1, respecting the specified limit and expiry.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long limit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.TryIncrementAsync(1, limit, expiry, cancellationToken);
        }

        /// <summary>
        /// Decrements the counter by one.
        /// </summary>
        /// <param name="counter">The counter instance.</param>
        /// <returns>The new value of the counter.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync() {
            return counter.DecrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>
        /// Decrements the counter by the specified amount using default expiry.
        /// </summary>
        /// <param name="counter">The counter instance.</param>
        /// <param name="amount">The amount to decrement.</param>
        /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
        /// <returns>The new value of the counter.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync(long amount, CancellationToken cancellationToken = default) {
            return counter.DecrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Gets the current value of the counter.
        /// </summary>
        /// <param name="counter">The counter instance.</param>
        /// <returns>The current value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> GetValueAsync() {
            return counter.GetValueAsync(default);
        }

        /// <summary>
        /// Resets the counter to zero and removes it from storage.
        /// </summary>
        /// <param name="counter">The counter instance.</param>
        /// <returns>A task that represents the asynchronous operation.</returns> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask ResetAsync() {
            return counter.ResetAsync(default);
        }
    }
}