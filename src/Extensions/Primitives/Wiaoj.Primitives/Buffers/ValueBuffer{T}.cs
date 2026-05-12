using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Wiaoj.Primitives.Buffers;
/// <summary>
/// Represents a temporary buffer that utilizes stack memory (via <see langword="stackalloc"/>) for small data
/// and falls back to the array pool for larger data. This structure is designed to minimize Garbage Collector (GC)
/// allocations in high-performance hot paths.
/// </summary>
/// <typeparam name="T">The type of items in the buffer. Must be an unmanaged type.</typeparam>
/// <remarks>
/// <para>
/// <strong>Usage Pattern:</strong>
/// This struct implements the "Hybrid Buffer" pattern. It requires an initial stack-allocated span
/// to be passed to the constructor. If the requested length fits in the stack span, it is used.
/// Otherwise, an array is rented from <see cref="ArrayPool{T}.Shared"/>.
/// </para>
/// <para>
/// <strong>Disposal:</strong>
/// Always use this struct within a <see langword="using" /> block or call <see cref="Dispose"/> manually to ensure
/// rented arrays are returned to the pool. If an <c>onDispose</c> callback was provided at construction,
/// it is invoked with the active span before the rented array is cleared and returned.
/// </para>
/// <para>
/// Being a <see langword="ref struct"/>, it cannot be stored on the heap or used in async methods across <see langword="await" /> points.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
[DebuggerDisplay("Length = {Length}")]
public ref struct ValueBuffer<T> where T : unmanaged {
    private T[]? _rented;
    private Span<T> _span;
    private readonly Action<Span<T>>? _onDispose;

    /// <summary>
    /// Initializes a new instance using the provided stack-allocated memory if sufficient;
    /// otherwise rents from the shared array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if <paramref name="minimumLength"/> fits.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueBuffer(int minimumLength, Span<T> initialBuffer) {
        if(minimumLength <= initialBuffer.Length) {
            this._rented = null;
            this._span = initialBuffer[..minimumLength];
        }
        else {
            this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
            this._span = this._rented.AsSpan(0, minimumLength);
        }
        this._onDispose = null;
    }

    /// <summary>
    /// Initializes a new instance using the provided stack-allocated memory if sufficient;
    /// otherwise rents from the shared array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    /// <param name="initialBuffer">A stack-allocated buffer to use if <paramref name="minimumLength"/> fits.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active <see cref="Span{T}"/> when <see cref="Dispose"/> is called,
    /// before the rented array is cleared and returned to the pool.
    /// Guaranteed to run as long as the buffer is used in a <see langword="using"/> block.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueBuffer(int minimumLength, Span<T> initialBuffer, Action<Span<T>> onDispose) {
        if(minimumLength <= initialBuffer.Length) {
            this._rented = null;
            this._span = initialBuffer[..minimumLength];
        }
        else {
            this._rented = ArrayPool<T>.Shared.Rent(minimumLength);
            this._span = this._rented.AsSpan(0, minimumLength);
        }
        this._onDispose = onDispose;
    }

    /// <summary>
    /// Initializes a new instance using EXACTLY the provided span. No renting occurs.
    /// </summary>
    /// <param name="initialBuffer">A stack-allocated buffer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueBuffer(Span<T> initialBuffer) {
        this._rented = null;
        this._span = initialBuffer;
        this._onDispose = null;
    }

    /// <summary>
    /// Initializes a new instance using EXACTLY the provided span. No renting occurs.
    /// </summary>
    /// <param name="initialBuffer">A stack-allocated buffer.</param>
    /// <param name="onDispose">
    /// A callback invoked with the active <see cref="Span{T}"/> when <see cref="Dispose"/> is called.
    /// </param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueBuffer(Span<T> initialBuffer, Action<Span<T>> onDispose) {
        this._rented = null;
        this._span = initialBuffer;
        this._onDispose = onDispose;
    }

    /// <summary>Gets a <see cref="Span{T}"/> representing the active memory region.</summary>
    public readonly Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._span;
    }

    /// <summary>Gets the number of elements in the buffer.</summary>
    public readonly int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._span.Length;
    }

    /// <summary>Gets a reference to the element at the specified index.</summary>
    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref this._span[index];
    }

    /// <summary>
    /// Forms a slice out of the current buffer starting at a specified index for a specified length.
    /// Enables C# Range operator (..) support directly on the buffer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly Span<T> Slice(int start, int length) {
        return this._span.Slice(start, length);
    }

    /// <summary>
    /// Returns a reference to the 0th element of the Span.
    /// Useful for pinning or unsafe interop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ref T GetPinnableReference() {
        return ref MemoryMarshal.GetReference(this._span);
    }

    // --- Conversions & Operators ---

    /// <summary>Implicitly converts a <see cref="ValueBuffer{T}"/> to a <see cref="Span{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(ValueBuffer<T> buffer) => buffer._span;

    /// <summary>Implicitly converts a <see cref="ValueBuffer{T}"/> to a <see cref="ReadOnlySpan{T}"/>.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(ValueBuffer<T> buffer) => buffer._span;

    /// <summary>
    /// Invokes the disposal callback (if any) with the active span,
    /// then clears and returns the rented array to the pool (if any).
    /// </summary>
    /// <remarks>
    /// Execution order:
    /// <list type="number">
    ///   <item><description><c>onDispose(span)</c> — logical slice at caller-requested length.</description></item>
    ///   <item><description><c>rented.AsSpan().Clear()</c> — full rented array, prevents pool data leaks.</description></item>
    ///   <item><description><c>ArrayPool.Return(rented)</c></description></item>
    /// </list>
    /// Safe to call multiple times; subsequent calls are no-ops.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {
        Span<T> span = this._span;
        this._span = default;

        T[]? toReturn = this._rented;
        this._rented = null;

        // 1. Caller callback — sees the logical slice, not the oversized rented array.
        this._onDispose?.Invoke(span);

        // 2. Clear + return the full rented array (covers any extra bytes beyond minimumLength).
        if(toReturn is not null) {
            toReturn.AsSpan().Clear();
            ArrayPool<T>.Shared.Return(toReturn);
        }
    }
}