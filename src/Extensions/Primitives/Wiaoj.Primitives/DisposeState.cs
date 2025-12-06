using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Wiaoj.Primitives;
/// <summary>
/// A high-performance, lock-free state tracker for IDisposable objects.
/// Unlike LifecycleManager, this is optimized specifically for the binary state (Active vs Disposed)
/// required by low-level primitives like Secret<T>.
/// </summary>
public sealed class DisposeState {
    private const byte StateActive = 0;
    private const byte StateDisposing = 1;
    private const byte StateDisposed = 2;

    private byte _state;

    public bool IsDisposed => Volatile.Read(ref _state) == StateDisposed;

    /// <summary>
    /// Attempts to transition from Active to Disposing.
    /// Returns true ONLY if the state was Active (ensures Dispose runs once).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryBeginDispose() {
        return Interlocked.CompareExchange(ref _state, StateDisposing, StateActive) == StateActive;
    }

    /// <summary>
    /// Marks the state as fully Disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetDisposed() {
        Volatile.Write(ref _state, StateDisposed);
    }

    /// <summary>
    /// Throws ObjectDisposedException if the object is disposing or disposed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public  void ThrowIfDisposingOrDisposed(string objectName) {
        if (Volatile.Read(in _state) != StateActive) {
            throw new ObjectDisposedException(objectName);
        }
    }
}