using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Testing;

/// <summary>
/// Fluent assertion extensions for <see cref="FakeCounterStorage"/> and <see cref="IDistributedCounter"/>.
/// </summary>
public static class CounterAssertionExtensions {

    /// <summary>
    /// Asserts that the storage contains the specified key with the expected value.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="key">The counter key to inspect.</param>
    /// <param name="expectedValue">The expected absolute value.</param>
    public static void ShouldHaveValue(this FakeCounterStorage storage, CounterKey key, long expectedValue) {
        Preca.ThrowIfNull(storage);

        IReadOnlyDictionary<string, CounterValue> snapshot = storage.Snapshot;
        if(!snapshot.TryGetValue(key.Value, out CounterValue actual)) {
            throw new InvalidOperationException($"Expected key '{key.Value}' to exist with value {expectedValue}, but the key was not found in storage.");
        }

        if(actual.Value != expectedValue) {
            throw new InvalidOperationException($"Expected key '{key.Value}' to have value {expectedValue}, but found {actual.Value}.");
        }
    }

    /// <summary>
    /// Asserts that the specified counter has the expected current value.
    /// </summary>
    /// <param name="counter">The counter instance to inspect.</param>
    /// <param name="expectedValue">The expected value.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async ValueTask ShouldHaveValueAsync(
        this IDistributedCounter counter,
        long expectedValue,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(counter);

        CounterValue actual = await counter.GetValueAsync(cancellationToken);
        if(actual.Value != expectedValue) {
            throw new InvalidOperationException($"Expected counter '{counter.Key.Value}' to have value {expectedValue}, but got {actual.Value}.");
        }
    }

    /// <summary>
    /// Asserts that a batch flush operation was recorded for the specified key.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="key">The counter key expected to have been flushed.</param>
    public static void ShouldHaveFlushed(this FakeCounterStorage storage, CounterKey key) {
        Preca.ThrowIfNull(storage);

        bool hasFlushed = storage.FlushedUpdates.Any(u => u.Key == key);
        if(!hasFlushed) {
            throw new InvalidOperationException($"Expected key '{key.Value}' to have been flushed, but no flush update was recorded for this key.");
        }
    }

    /// <summary>
    /// Asserts that a batch flush operation was recorded for the specified key with a specific delta amount.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="key">The counter key expected to have been flushed.</param>
    /// <param name="expectedDelta">The expected delta amount that was flushed.</param>
    public static void ShouldHaveFlushed(this FakeCounterStorage storage, CounterKey key, long expectedDelta) {
        Preca.ThrowIfNull(storage);

        bool hasMatchingDelta = storage.FlushedUpdates.Any(u => u.Key == key && u.Amount == expectedDelta);
        if(!hasMatchingDelta) {
            throw new InvalidOperationException($"Expected key '{key.Value}' to have been flushed with delta {expectedDelta}, but no matching update was found in flush history.");
        }
    }

    /// <summary>
    /// Asserts that the total number of batch flush calls equals the expected count.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="expectedCount">The expected number of batch flush invocations.</param>
    public static void ShouldHaveBatchFlushCount(this FakeCounterStorage storage, int expectedCount) {
        Preca.ThrowIfNull(storage);

        if(storage.BatchIncrementCallCount != expectedCount) {
            throw new InvalidOperationException($"Expected {expectedCount} batch flush calls, but recorded {storage.BatchIncrementCallCount}.");
        }
    }
}