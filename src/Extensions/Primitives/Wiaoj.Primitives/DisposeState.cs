using System.Runtime.CompilerServices;
using Wiaoj.Concurrency;

namespace Wiaoj.Primitives;

/// <summary>
/// A high-performance, lock-free state tracker for <see cref="IDisposable"/> and <see cref="IAsyncDisposable"/> objects.
/// </summary>
/// <remarks>
/// This class is specifically optimized for binary state transitions (Active vs. Disposing vs. Disposed).
/// It provides zero-allocation synchronous fast-paths while supporting non-blocking asynchronous coordination.
/// </remarks>
public sealed class DisposeState {
    private const byte StateActive = 0;
    private const byte StateDisposing = 1;
    private const byte StateDisposed = 2;

    private byte _state;
    private volatile TaskCompletionSource? _tcs;

    /// <summary>
    /// Gets a value indicating whether the object has been fully disposed.
    /// </summary>
    public bool IsDisposed => Atomic.Read(ref this._state) == StateDisposed;

    /// <summary>
    /// Gets a value indicating whether the object is currently in the process of disposing or is already disposed.
    /// </summary>
    public bool IsDisposingOrDisposed => Atomic.Read(ref this._state) != StateActive;

    /// <summary>
    /// Attempts to transition the state from <see cref="StateActive"/> to <see cref="StateDisposing"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the transition was successful (i.e., this caller won the race to perform disposal); 
    /// otherwise, <see langword="false"/> if it was already disposing or disposed.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryBeginDispose() {
        return Atomic.TryCompareExchange(ref this._state, StateDisposing, StateActive);
    }

    /// <summary>
    /// Marks the state as <see cref="StateDisposed"/> and signals any asynchronous waiters that disposal is complete.
    /// </summary>
    /// <remarks>
    /// This MUST be called inside a <c>finally</c> block after all cleanup logic has executed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDisposed() {
        Atomic.Write(ref this._state, StateDisposed);
        this._tcs?.TrySetResult();
    }

    /// <summary>
    /// Asynchronously waits until the disposal process has fully completed (<see cref="SetDisposed"/> is called).
    /// </summary>
    /// <param name="cancellationToken">An optional token to cancel the wait operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when disposal is finished.</returns>
    public ValueTask WaitForDisposedAsync(CancellationToken cancellationToken = default) {
        // Fast-path: Already fully disposed (Zero allocation, no state machine)
        if(this.IsDisposed) {
            return ValueTask.CompletedTask;
        }

        TaskCompletionSource tcs = GetOrCreateTcs();

        // Double-check race condition: SetDisposed might have executed just before TCS assignment
        if(this.IsDisposed) {
            tcs.TrySetResult();
            return ValueTask.CompletedTask;
        }

        if(cancellationToken.CanBeCanceled) {
            return new ValueTask(tcs.Task.WaitAsync(cancellationToken));
        }

        return new ValueTask(tcs.Task);
    }

    /// <summary>
    /// Validates the object state and throws an <see cref="ObjectDisposedException"/> if the object 
    /// is currently in the disposing or disposed state.
    /// </summary>
    /// <param name="objectName">The name of the object to be included in the exception message.</param>
    /// <exception cref="ObjectDisposedException">Thrown if the state is not <see cref="StateActive"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ThrowIfDisposingOrDisposed(string objectName) {
        ObjectDisposedException.ThrowIf(Atomic.Read(in this._state) != StateActive, objectName);
    }

    // ── Private Helpers ───────────────────────────────────────────────────────

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TaskCompletionSource GetOrCreateTcs() {
        if(this._tcs is not null) return this._tcs;

        TaskCompletionSource newTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        return Interlocked.CompareExchange(ref this._tcs, newTcs, null) ?? newTcs;
    }
}