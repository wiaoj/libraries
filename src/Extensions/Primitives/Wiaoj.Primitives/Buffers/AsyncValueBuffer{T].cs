using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Buffers;
/// <summary>
/// Async-safe counterpart of <see cref="ValueBuffer{T}"/>. Represents a temporary buffer backed by
/// the shared <see cref="ArrayPool{T}"/> (with an optional caller-provided pre-allocated buffer),
/// designed to be safely used across <see langword="await"/> points inside async methods.
/// </summary>
/// <typeparam name="T">The type of items in the buffer. Must be an unmanaged type.</typeparam>
/// <remarks>
/// <para>
/// <strong>Why not <see cref="ValueBuffer{T}"/>?</strong>
/// <see cref="ValueBuffer{T}"/> is a <see langword="ref struct"/> and therefore cannot be declared as a local
/// in an async method, nor can it survive an <see langword="await"/> boundary (CS4012). It also cannot use
/// <see langword="stackalloc"/> memory here, since stack memory cannot outlive an await point either —
/// so this type only supports pool-rented or heap-allocated backing storage via <see cref="Memory{T}"/>.
/// </para>
/// <para>
/// <strong>Usage Pattern:</strong>
/// Implemented as a sealed class (not a struct) to avoid copy-semantics hazards: async state machines
/// hoist locals into fields, and a mutable struct holding a rented array could be copied, leading to the
/// array being returned to the pool more than once, or a copy outliving disposal. A reference type sidesteps this.
/// </para>
/// <para>
/// <strong>Disposal:</strong>
/// Always use within an <see langword="await using"/> block, or call <see cref="DisposeAsync"/> manually.
/// A synchronous <see cref="Dispose"/> is also provided for symmetry, but prefer <see cref="DisposeAsync"/>
/// when an async callback was supplied.
/// </para>
/// </remarks>
[DebuggerDisplay("Length = {Length}")]
public sealed class AsyncValueBuffer<T> : IAsyncDisposable, IDisposable where T : unmanaged {
    private T[]? _rented;
    private Memory<T> _memory;
    private readonly Action<Memory<T>>? _onDispose;
    private readonly Func<Memory<T>, ValueTask>? _onDisposeAsync;
    private readonly DisposeState _disposeState = new();

    /// <summary>
    /// Initializes a new instance, always renting <paramref name="minimumLength"/> items from
    /// <see cref="ArrayPool{T}.Shared"/>. Use this overload when no small pre-allocated buffer is available.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncValueBuffer(int minimumLength) {
        this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
        this._memory = this._rented.AsMemory(0, minimumLength);
    }

    /// <summary>
    /// Initializes a new instance using the provided pre-allocated memory if sufficient;
    /// otherwise rents from the shared array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    /// <param name="initialBuffer">
    /// A caller-owned, heap-allocated buffer to use if <paramref name="minimumLength"/> fits.
    /// Unlike <see cref="ValueBuffer{T}"/>, this cannot be <see langword="stackalloc"/> memory.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncValueBuffer(int minimumLength, Memory<T> initialBuffer) {
        if(minimumLength <= initialBuffer.Length) {
            this._rented = null;
            this._memory = initialBuffer[..minimumLength];
        }
        else {
            this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
            this._memory = this._rented.AsMemory(0, minimumLength);
        }
    }

    /// <summary>
    /// Initializes a new instance, always renting from the pool, with an async callback invoked on disposal.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    /// <param name="onDisposeAsync">
    /// A callback invoked with the active <see cref="Memory{T}"/> when <see cref="DisposeAsync"/> is called,
    /// before the rented array is cleared and returned to the pool.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncValueBuffer(int minimumLength, Func<Memory<T>, ValueTask> onDisposeAsync) {
        this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
        this._memory = this._rented.AsMemory(0, minimumLength);
        this._onDisposeAsync = onDisposeAsync;
    }

    /// <summary>
    /// Initializes a new instance using the provided pre-allocated memory if sufficient;
    /// otherwise rents from the shared array pool, with an async callback invoked on disposal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncValueBuffer(int minimumLength, Memory<T> initialBuffer, Func<Memory<T>, ValueTask> onDisposeAsync) {
        if(minimumLength <= initialBuffer.Length) {
            this._rented = null;
            this._memory = initialBuffer[..minimumLength];
        }
        else {
            this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
            this._memory = this._rented.AsMemory(0, minimumLength);
        }
        this._onDisposeAsync = onDisposeAsync;
    }

    /// <summary>
    /// Initializes a new instance, always renting from the pool, with a synchronous callback invoked on disposal.
    /// Use this when the disposal action doesn't need to await anything (e.g. logging, sync copy).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public AsyncValueBuffer(int minimumLength, Action<Memory<T>> onDispose) {
        this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
        this._memory = this._rented.AsMemory(0, minimumLength);
        this._onDispose = onDispose;
    }

    /// <summary>Gets a value indicating whether this buffer has been disposed (or is disposing).</summary>
    public bool IsDisposed {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._disposeState.IsDisposingOrDisposed;
    }

    /// <summary>Gets a <see cref="Memory{T}"/> representing the active memory region.</summary>
    public Memory<T> Memory {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(AsyncValueBuffer<T>));
            return this._memory;
        }
    }

    /// <summary>Gets a <see cref="Span{T}"/> view over the active memory region. Do not hold across an await point.</summary>
    public Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(AsyncValueBuffer<T>));
            return this._memory.Span;
        }
    }

    /// <summary>Gets the number of elements in the buffer.</summary>
    public int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._memory.Length;
    }

    /// <summary>Gets or sets the element at the specified index.</summary>
    public T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._memory.Span[index];
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => this._memory.Span[index] = value;
    }

    /// <summary>Forms a slice out of the current buffer starting at a specified index for a specified length.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Memory<T> Slice(int start, int length) {
        return this._memory.Slice(start, length);
    }

    /// <summary>Implicitly converts an <see cref="AsyncValueBuffer{T}"/> to a <see cref="Memory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Memory<T>(AsyncValueBuffer<T> buffer) {
        return buffer._memory;
    }

    /// <summary>Implicitly converts an <see cref="AsyncValueBuffer{T}"/> to a <see cref="ReadOnlyMemory{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlyMemory<T>(AsyncValueBuffer<T> buffer) {
        return buffer._memory;
    }

    /// <summary>
    /// The single cleanup path. <see cref="DisposeState.TryBeginDispose"/> guarantees only one caller
    /// (sync or async, concurrent or repeated) ever executes the body below — no duplicated bookkeeping,
    /// no double-return-to-pool.
    /// Invokes the async disposal callback (if any), then the sync callback (if any),
    /// then clears and returns the rented array to the pool (if any).
    /// Safe to call multiple times, and safe to call concurrently; only the winner runs cleanup.
    /// </summary>
    public async ValueTask DisposeAsync() {
        if(!this._disposeState.TryBeginDispose()) return;

        try {
            Memory<T> memory = this._memory;
            this._memory = default;

            T[]? toReturn = this._rented;
            this._rented = null;

            if(this._onDisposeAsync is not null) {
                await this._onDisposeAsync(memory).ConfigureAwait(false);
            }
            this._onDispose?.Invoke(memory);

            if(toReturn is not null) {
                toReturn.AsSpan().Clear();
                ArrayPool<T>.Shared.Return(toReturn);
            }
        }
        finally {
            this._disposeState.SetDisposed();
        }
    }

    /// <summary>
    /// Synchronous disposal path. Simply delegates to <see cref="DisposeAsync"/> and blocks on it
    /// (<c>.GetAwaiter().GetResult()</c>) — there is no separate cleanup logic to keep in sync.
    /// If an async callback was supplied, prefer calling <see cref="DisposeAsync"/> directly to avoid blocking.
    /// Safe to call multiple times, and safe to call even if <see cref="DisposeAsync"/> already ran (no-op).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}