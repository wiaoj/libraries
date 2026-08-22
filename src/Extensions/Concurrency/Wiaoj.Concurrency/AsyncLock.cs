using System.Diagnostics;

namespace Wiaoj.Concurrency;
/// <summary>
/// An asynchronous, non-reentrant mutual exclusion lock.
/// </summary>
/// <remarks>
/// This lock provides a mechanism to protect a critical section of code from concurrent access
/// across asynchronous operations. It is essential to release the lock by disposing of the returned
/// <see cref="Scope"/> struct, preferably within a 'using' block.
/// </remarks>
/// <example>
/// <code>
/// private readonly AsyncLock _lock = new();
///
/// public async Task MySafeMethodAsync(CancellationToken cancellationToken)
/// {
///     using (await _lock.LockAsync(cancellationToken))
///     {
///         // Critical section: only one task can be here at a time.
///         await DoSomethingAsync();
///     }
/// }
/// </code>
/// </example>
[DebuggerDisplay("IsLocked = {IsLocked}")]
public sealed class AsyncLock {
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Gets a value indicating whether the lock is currently held by any task.
    /// </summary>
    /// <remarks>This property is intended for monitoring and debugging. Its value can change immediately after being read.</remarks>
    public bool IsLocked => this._semaphore.CurrentCount == 0;

    /// <summary>
    /// Asynchronously acquires the lock.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation requests while waiting for the lock.</param>
    /// <returns>
    /// A <see cref="ValueTask{Releaser}"/> that completes when the lock is acquired.
    /// The returned <see cref="Scope"/> must be disposed to release the lock.
    /// </returns>
    public ValueTask<Scope> LockAsync(CancellationToken cancellationToken = default) {
        if (this._semaphore.Wait(0, cancellationToken))
            return new ValueTask<Scope>(new Scope(this));

        return LockAsyncSlowPath(cancellationToken);
    }

    private async ValueTask<Scope> LockAsyncSlowPath(CancellationToken cancellationToken) {
        await this._semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Scope(this);
    }

    /// <summary>
    /// A disposable struct responsible for releasing the <see cref="AsyncLock"/>.
    /// </summary>
    /// <remarks>
    /// This is a readonly struct to prevent accidental copying and ensure correct lock release behavior.
    /// It avoids heap allocation when the lock is acquired.
    /// </remarks>
    public readonly struct Scope : IDisposable {
        private readonly AsyncLock? _lockToRelease;

        /// <summary>
        /// Initializes a new instance of the <see cref="Scope"/> struct.
        /// </summary>
        /// <param name="lockToRelease">The lock to release upon disposal.</param>
        internal Scope(AsyncLock lockToRelease) {
            this._lockToRelease = lockToRelease;
        }

        /// <summary>
        /// Releases the held lock.
        /// </summary>
        public void Dispose() {
            // If _lockToRelease is null (e.g., from default(Releaser)), this does nothing.
            this._lockToRelease?._semaphore.Release();
        }
    }
}