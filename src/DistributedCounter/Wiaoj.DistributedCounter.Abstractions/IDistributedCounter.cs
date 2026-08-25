using System.Runtime.CompilerServices;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// Represents a high-level distributed counter instance.
/// Defines the core contract without default values to simplify implementation.
/// </summary>
/// <remarks>
/// Deliberately carries zero default parameter values. C# resolves default values against the
/// *static type* of the call-site reference, not the runtime implementation — so a default
/// declared here would behave differently (or fail to compile) depending on whether a caller
/// holds an <see cref="IDistributedCounter"/> reference or a concrete implementation's own type.
/// All "amount defaults to 1" / "expiry defaults to infinite" convenience lives exclusively in
/// <see cref="DistributedCounterExtensions"/> instead, where it's resolved statically and behaves
/// identically regardless of the reference's static type.
/// </remarks>
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
    /// Atomically compares the counter's current value with an expected value, and if they match, sets the new value.
    /// </summary>
    /// <param name="expectedValue">The value that is expected to be in storage.</param>
    /// <param name="newValue">The new value to set if matching.</param>
    /// <param name="expiry">The expiration policy to apply.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> if the replacement succeeded; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> TryCompareExchangeAsync(
        CounterValue expectedValue,
        CounterValue newValue,
        CounterExpiry expiry,
        CancellationToken cancellationToken);

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
     
    /// <summary>
    /// Atomically overwrites the counter with an absolute value and updates its time-to-live expiration.
    /// </summary>
    ValueTask SetAsync(long value, CounterExpiry expiry = default, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides convenient extension methods for <see cref="IDistributedCounter"/>. Every overload
/// here is a pure delegation to the four-argument interface members above — no new behavior,
/// just filling in "amount defaults to 1" / "expiry defaults to <see cref="CounterExpiry.Infinite"/>"
/// for the common call shapes, distinguished by parameter type (rather than a single method with
/// several optional parameters) so callers can pass overrides positionally instead of needing
/// named arguments — e.g. <c>counter.IncrementAsync(myExpiry)</c> instead of
/// <c>counter.IncrementAsync(expiry: myExpiry)</c>.
/// </summary>
public static partial class DistributedCounterExtensions {
    extension(IDistributedCounter counter) {
        // --- Increment ---------------------------------------------------

        /// <summary>Increments the counter by 1.</summary> 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync() {
            return counter.IncrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>Increments the counter by 1 with a cancellation token.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(CancellationToken cancellationToken) {
            return counter.IncrementAsync(1, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>Increments the counter by 1 with a specific expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.IncrementAsync(1, expiry, cancellationToken);
        }

        /// <summary>Increments the counter by the specified amount using default (infinite) expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(long amount, CancellationToken cancellationToken = default) {
            return counter.IncrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Increments the counter by the specified amount with a specific expiry, without requiring
        /// an explicit cancellation token. Symmetric with the other three-argument overloads above —
        /// previously this combination required calling the raw interface member and passing
        /// <see langword="default"/> for the token explicitly.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.IncrementAsync(amount, expiry, cancellationToken);
        }

        // --- TryIncrement --------------------------------------------------

        /// <summary>Attempts to increment the counter by 1, respecting the specified limit.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long limit) {
            return counter.TryIncrementAsync(1, limit, CounterExpiry.Infinite, default);
        }

        /// <summary>Attempts to increment the counter by 1, respecting the specified limit and expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long limit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.TryIncrementAsync(1, limit, expiry, cancellationToken);
        }

        /// <summary>
        /// Attempts to increment the counter by the specified amount (cost), respecting the limit,
        /// using default (infinite) expiry. Previously missing — the only way to use a non-default
        /// cost was the raw four-argument interface member.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CancellationToken cancellationToken = default) {
            return counter.TryIncrementAsync(amount, limit, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Attempts to increment the counter by the specified amount (cost), respecting the limit
        /// and expiry, without requiring an explicit cancellation token.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.TryIncrementAsync(amount, limit, expiry, cancellationToken);
        }

        // --- Decrement ---------------------------------------------------

        /// <summary>Decrements the counter by one.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync() {
            return counter.DecrementAsync(1, CounterExpiry.Infinite, default);
        }

        /// <summary>Decrements the counter by the specified amount using default (infinite) expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync(long amount, CancellationToken cancellationToken = default) {
            return counter.DecrementAsync(amount, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>
        /// Decrements the counter by 1 with a specific expiry. Previously missing — this was the
        /// one Increment-family shape (amount=1, custom expiry) with no Decrement counterpart.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync(CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.DecrementAsync(1, expiry, cancellationToken);
        }

        /// <summary>
        /// Decrements the counter by the specified amount with a specific expiry, without requiring
        /// an explicit cancellation token.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.DecrementAsync(amount, expiry, cancellationToken);
        }

        // --- TryDecrement --------------------------------------------------
        // Previously entirely absent from this file — every shape below mirrors its TryIncrement
        // counterpart above. A caller needing "don't let this drop below N" (e.g. a quota /
        // concurrency-slot counter) had no convenience overload at all before this.

        /// <summary>Attempts to decrement the counter by 1, respecting the specified minimum limit.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync(long minLimit) {
            return counter.TryDecrementAsync(1, minLimit, CounterExpiry.Infinite, default);
        }

        /// <summary>Attempts to decrement the counter by 1, respecting the specified minimum limit and expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync(long minLimit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.TryDecrementAsync(1, minLimit, expiry, cancellationToken);
        }

        /// <summary>Attempts to decrement the counter by the specified amount, respecting the minimum limit, using default (infinite) expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CancellationToken cancellationToken = default) {
            return counter.TryDecrementAsync(amount, minLimit, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>Attempts to decrement the counter by the specified amount, respecting the minimum limit and expiry, without requiring an explicit cancellation token.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken = default) {
            return counter.TryDecrementAsync(amount, minLimit, expiry, cancellationToken);
        }

        /// <summary>Attempts to replace the counter value if matching the expected value, using infinite expiration.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> TryCompareExchangeAsync(
            CounterValue expectedValue,
            CounterValue newValue,
            CancellationToken cancellationToken = default) {
            return counter.TryCompareExchangeAsync(expectedValue, newValue, CounterExpiry.Infinite, cancellationToken);
        }

        /// <summary>Attempts to replace the counter value if matching the expected value with a specific expiry.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<bool> TryCompareExchangeAsync(
            CounterValue expectedValue,
            CounterValue newValue,
            CounterExpiry expiry,
            CancellationToken cancellationToken = default) {
            return counter.TryCompareExchangeAsync(expectedValue, newValue, expiry, cancellationToken);
        }

        // --- Misc ---------------------------------------------------

        /// <summary>Gets the current value of the counter.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask<CounterValue> GetValueAsync() {
            return counter.GetValueAsync(default);
        }

        /// <summary>Resets the counter to zero and removes it from storage.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ValueTask ResetAsync() {
            return counter.ResetAsync(default);
        }
    }
}