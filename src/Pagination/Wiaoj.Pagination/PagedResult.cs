using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Wiaoj.Pagination.JsonConverters;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Collections;

namespace Wiaoj.Pagination;

/// <summary>
/// Represents an immutable, zero-allocation container combining paginated items with their corresponding metadata.
/// </summary>
/// <typeparam name="T">The type of elements in the paginated collection.</typeparam>
/// <remarks>
/// This struct leverages <see cref="EquatableArray{T}"/> to provide SIMD-accelerated,
/// content-based value equality without allocating collection wrappers on the managed heap.
/// </remarks>
[DebuggerDisplay("Count = {Count}, {Metadata}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(PagedResultJsonConverterFactory))]
public readonly record struct PagedResult<T> :
    IEquatable<PagedResult<T>>,
    IEqualityOperators<PagedResult<T>, PagedResult<T>, bool> {

    /// <summary>
    /// Gets an empty <see cref="PagedResult{T}"/> instance.
    /// </summary>
    public static readonly PagedResult<T> Empty = default;

    /// <summary>
    /// Gets the paginated items collection.
    /// </summary>
    public EquatableArray<T> Items { get; }

    /// <summary>
    /// Gets the pagination metadata.
    /// </summary>
    public PageMetadata Metadata { get; }

    /// <summary>
    /// Gets the number of items in the current page.
    /// </summary>
    public int Count => this.Items.Count;

    /// <summary>
    /// Gets a value indicating whether the current page contains no items.
    /// </summary>
    public bool IsEmpty => this.Items.Count == 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="PagedResult{T}"/> struct.
    /// </summary>
    /// <param name="items">The items collection. Wraps into an immutable equatable array.</param>
    /// <param name="metadata">The pagination metadata.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PagedResult(EquatableArray<T> items, PageMetadata metadata) {
        this.Items = items;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Returns a zero-allocation <see cref="ReadOnlySpan{T}"/> view over the current items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan() {
        return this.Items.AsSpan();
    }

    /// <summary>
    /// Projects each element of the paginated result into a new form while preserving the existing metadata.
    /// </summary>
    /// <typeparam name="TResult">The target element type.</typeparam>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A new <see cref="PagedResult{TResult}"/> containing the mapped elements.</returns>
    public PagedResult<TResult> Select<TResult>(Func<T, TResult> selector) {
        Preca.ThrowIfNull(selector);

        if(this.IsEmpty) {
            return PagedResult<TResult>.Empty;
        }

        ReadOnlySpan<T> span = this.Items.AsSpan();
        TResult[] mapped = new TResult[span.Length];

        for(int i = 0; i < span.Length; i++) {
            mapped[i] = selector(span[i]);
        }

        return new PagedResult<TResult>(mapped, this.Metadata);
    }

    /// <summary>
    /// Deconstructs the <see cref="PagedResult{T}"/> into its items and metadata components.
    /// </summary>
    /// <param name="items">The items collection.</param>
    /// <param name="metadata">The metadata instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out EquatableArray<T> items, out PageMetadata metadata) {
        items = this.Items;
        metadata = this.Metadata;
    }
}