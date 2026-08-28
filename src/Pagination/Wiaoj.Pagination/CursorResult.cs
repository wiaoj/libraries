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
/// Represents an immutable, zero-allocation container combining keyset paginated items with their corresponding cursor metadata.
/// </summary>
/// <typeparam name="T">The type of elements in the paginated collection.</typeparam>
[DebuggerDisplay("Count = {Count}, {Metadata}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(CursorResultJsonConverterFactory))]
public readonly record struct CursorResult<T> :
    IEquatable<CursorResult<T>>,
    IEqualityOperators<CursorResult<T>, CursorResult<T>, bool> {

    /// <summary>
    /// Gets an empty <see cref="CursorResult{T}"/> instance.
    /// </summary>
    public static readonly CursorResult<T> Empty = default;

    /// <summary>
    /// Gets the paginated items collection.
    /// </summary>
    public EquatableArray<T> Items { get; }

    /// <summary>
    /// Gets the keyset pagination metadata.
    /// </summary>
    public CursorMetadata Metadata { get; }

    /// <summary>
    /// Gets the number of items in the current page window.
    /// </summary>
    public int Count => this.Items.Count;

    /// <summary>
    /// Gets a value indicating whether the current page window contains no items.
    /// </summary>
    public bool IsEmpty => this.Items.IsEmpty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CursorResult{T}"/> struct.
    /// </summary>
    /// <param name="items">The items collection.</param>
    /// <param name="metadata">The cursor metadata.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public CursorResult(EquatableArray<T> items, CursorMetadata metadata) {
        this.Items = items;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Returns a zero-allocation <see cref="ReadOnlySpan{T}"/> view over the current items.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<T> AsSpan() => this.Items.AsSpan();

    /// <summary>
    /// Projects each element of the cursor result into a new form while preserving the existing cursor metadata.
    /// </summary>
    /// <typeparam name="TResult">The target element type.</typeparam>
    /// <param name="selector">A transform function to apply to each element.</param>
    /// <returns>A new <see cref="CursorResult{TResult}"/> containing the mapped elements.</returns>
    public CursorResult<TResult> Select<TResult>(Func<T, TResult> selector) {
        Preca.ThrowIfNull(selector);

        if(this.IsEmpty) {
            return CursorResult<TResult>.Empty;
        }

        ReadOnlySpan<T> span = this.Items.AsSpan();
        TResult[] mapped = new TResult[span.Length];

        for(int i = 0; i < span.Length; i++) {
            mapped[i] = selector(span[i]);
        }

        return new CursorResult<TResult>(mapped, this.Metadata);
    }

    /// <summary>
    /// Deconstructs the <see cref="CursorResult{T}"/> into its items and cursor metadata components.
    /// </summary>
    /// <param name="items">The items collection.</param>
    /// <param name="metadata">The cursor metadata instance.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Deconstruct(out EquatableArray<T> items, out CursorMetadata metadata) {
        items = this.Items;
        metadata = this.Metadata;
    }
}