namespace Wiaoj.Concurrency;

/// <summary>
/// Provides an asynchronous auto-reset event, releasing a single waiting task upon being set.
/// </summary>
/// <remarks>
/// This class is a thread-safe, asynchronous equivalent of <see cref="System.Threading.AutoResetEvent"/>.
/// When it is set, only one waiting task is released, and the event automatically resets to a non-signaled state.
/// </remarks>
public class AsyncAutoResetEvent {
    // A queue of TaskCompletionSource objects representing the waiting tasks.
    private readonly Queue<TaskCompletionSource<bool>> _waiters = new();

    // A flag indicating whether the event is currently in a set (signaled) state.
    private bool _isSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsyncAutoResetEvent"/> class.
    /// </summary>
    /// <param name="initialState">
    /// If true, the event is initially in a set state; otherwise, it is in an unset state.
    /// </param>
    public AsyncAutoResetEvent(bool initialState = false) {
        this._isSet = initialState;
    }

    /// <summary>
    /// Asynchronously waits for the event to be set.
    /// </summary>
    /// <returns>A task that completes when the event is set.</returns>
    public Task WaitAsync(CancellationToken cancellationToken = default) {
        // The lock protects the _waiters queue and the _isSet flag.
        lock (this._waiters) {
            // If the event is already set, we can complete immediately.
            if (this._isSet) {
                // Consume the signal and return a completed task.
                this._isSet = false;
                return Task.CompletedTask;
            }

            // If the event is not set, create a new waiter and add it to the queue.
            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            // If cancellation is requested before we even wait, complete the task as cancelled.
            if (cancellationToken.IsCancellationRequested) {
                tcs.SetCanceled(cancellationToken);
            }
            else {
                // Register a callback to cancel the TCS if the token is triggered.
                cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            }

            this._waiters.Enqueue(tcs);
            return tcs.Task;
        }
    }

    /// <summary>
    /// Sets the state of the event to signaled, allowing one waiting task to proceed.
    /// </summary>
    /// <remarks>
    /// If any tasks are waiting, the first one in the queue is released, and the event remains non-signaled.
    /// If no tasks are waiting, the event becomes signaled and will release the next task that waits.
    /// </remarks>
    public void Set() {
        TaskCompletionSource<bool>? toRelease = null;

        lock (this._waiters) {
            // If there are tasks waiting in the queue, dequeue the next one to release it.
            if (this._waiters.Count > 0) {
                toRelease = this._waiters.Dequeue();
            }
            // If no tasks are waiting, set the flag so the next waiter completes immediately.
            else if (!this._isSet) {
                this._isSet = true;
            }
        }

        // Release the waiter outside the lock to avoid potential deadlocks.
        toRelease?.TrySetResult(true);
    }
}