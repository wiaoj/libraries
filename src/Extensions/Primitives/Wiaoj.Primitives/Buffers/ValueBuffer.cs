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
/// rented arrays are returned to the pool.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ValueBuffer{T}"/> struct.
    /// Uses the provided stack-allocated memory if sufficient; otherwise, rents from the shared array pool.
    /// </summary>
    /// <param name="minimumLength">The minimum required length of the buffer.</param>
    /// <param name="initialBuffer">
    /// A stack-allocated buffer (e.g., <c>stackalloc T[256]</c>) to use if <paramref name="minimumLength"/> fits.
    /// </param>
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
    }

    /// <summary>
    /// Gets a <see cref="Span{T}"/> representing the active memory region.
    /// </summary>
    public readonly Span<T> Span {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._span;
    }

    /// <summary>
    /// Gets the number of elements in the buffer.
    /// </summary>
    public readonly int Length {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => this._span.Length;
    }

    /// <summary>
    /// Gets a reference to the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element.</param>
    /// <returns>A reference to the element.</returns>
    public ref T this[int index] {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ref this._span[index];
    }

    /// <summary>
    /// Returns the buffer to the pool if it was rented.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {
        // Safe double-dispose guard
        T[]? toReturn = this._rented;
        this._rented = null; // Clear field to prevent double return

        if(toReturn is not null) {
            toReturn.AsSpan().Clear(); // Clear the rented array to prevent data leaks
            ArrayPool<T>.Shared.Return(toReturn);
        }
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

    /// <summary>
    /// Implicitly converts a <see cref="ValueBuffer{T}"/> to a <see cref="Span{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Span<T>(ValueBuffer<T> buffer) {
        return buffer._span;
    }

    /// <summary>
    /// Implicitly converts a <see cref="ValueBuffer{T}"/> to a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator ReadOnlySpan<T>(ValueBuffer<T> buffer) {
        return buffer._span;
    }
}