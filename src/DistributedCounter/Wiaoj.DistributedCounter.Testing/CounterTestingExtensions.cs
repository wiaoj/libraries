using Microsoft.Extensions.Time.Testing;
using Wiaoj.Preconditions;

namespace Wiaoj.DistributedCounter.Testing;

/// <summary>
/// Test execution and synchronization extension helpers for <see cref="FakeCounterStorage"/> and background workers.
/// </summary>
public static class CounterTestingExtensions {

    /// <summary>
    /// Pumps time forward on <paramref name="timeProvider"/> in increments of <paramref name="step"/>
    /// until the background auto-flush worker triggers and completes a batch flush operation, using a default 5-second timeout.
    /// Handles <see cref="PeriodicTimer"/> thread-pool arming synchronization automatically without manual polling.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="timeProvider">The fake time provider driving the background timer.</param>
    /// <param name="step">The time duration to advance on each pump iteration (typically matching the flush interval).</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static Task WaitForNextFlushAsync(
        this FakeCounterStorage storage,
        FakeTimeProvider timeProvider,
        TimeSpan step,
        CancellationToken cancellationToken = default) {
        return storage.WaitForNextFlushAsync(timeProvider, step, TimeSpan.FromSeconds(5), cancellationToken);
    }

    /// <summary>
    /// Pumps time forward on <paramref name="timeProvider"/> in increments of <paramref name="step"/>
    /// until the background auto-flush worker triggers and completes a batch flush operation, respecting the specified <paramref name="timeout"/>.
    /// Handles <see cref="PeriodicTimer"/> thread-pool arming synchronization automatically without manual polling.
    /// </summary>
    /// <param name="storage">The fake counter storage instance.</param>
    /// <param name="timeProvider">The fake time provider driving the background timer.</param>
    /// <param name="step">The time duration to advance on each pump iteration (typically matching the flush interval).</param>
    /// <param name="timeout">The maximum real-time duration to wait before aborting.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    public static async Task WaitForNextFlushAsync(
        this FakeCounterStorage storage,
        FakeTimeProvider timeProvider,
        TimeSpan step,
        TimeSpan timeout,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(storage);
        Preca.ThrowIfNull(timeProvider);

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        Task flushSignal = storage.WaitForBatchIncrementAsync(Timeout.InfiniteTimeSpan, timeoutCts.Token);

        while(!flushSignal.IsCompleted) {
            timeProvider.Advance(step);
            await Task.WhenAny(flushSignal, Task.Delay(10, cancellationToken)).ConfigureAwait(false);
        }

        await flushSignal.ConfigureAwait(false);
    }
}