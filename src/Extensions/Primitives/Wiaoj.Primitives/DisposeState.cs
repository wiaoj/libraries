using System.Runtime.CompilerServices;
using Wiaoj.Concurrency;

namespace Wiaoj.Primitives;
/// <summary>
/// A high-performance, lock-free state tracker for <see cref="IDisposable"/> objects.
/// </summary>
/// <remarks>
/// This class is specifically optimized for binary state transitions (Active vs. Disposed), 
/// making it ideal for low-level primitives like <see cref="Secret{T}"/>.
/// </remarks>
public sealed class DisposeState {
    private const byte StateActive = 0;
    private const byte StateDisposing = 1;
    private const byte StateDisposed = 2;

    private byte _state;

    /// <summary>
    /// Gets a value indicating whether the object has been fully disposed.
    /// </summary>
    public bool IsDisposed => Atomic.Read(ref this._state) == StateDisposed;

    /// <summary>
    /// Attempts to transition the state from <see cref="StateActive"/> to <see cref="StateDisposing"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the transition was successful (i.e., the object was in the Active state); 
    /// otherwise, <see langword="false"/> if it was already disposing or disposed.
    /// </returns>
    /// <remarks>
    /// This method ensures that the disposal logic is executed exactly once in multi-threaded scenarios.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryBeginDispose() {
        return Atomic.CompareExchange(ref this._state, StateDisposing, StateActive);
    }

    /// <summary>
    /// Marks the current state as <see cref="StateDisposed"/>.
    /// </summary>
    /// <remarks>
    /// This should be called only after the cleanup logic has been successfully executed.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDisposed() {
        Atomic.Write(ref this._state, StateDisposed);
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
}