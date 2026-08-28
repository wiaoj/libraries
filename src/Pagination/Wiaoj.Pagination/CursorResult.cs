using System.Collections;
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
/// <remarks>
/// Supports C# collection expressions (e.g. <c>CursorResult&lt;int&gt; result = [1, 2, 3];</c> or
/// <c>CursorResult&lt;int&gt; empty = [];</c>) via <see cref="CursorResult.Create{T}(ReadOnlySpan{T})"/>.
/// Instances created this way always carry <see cref="CursorMetadata.Empty"/>, since a collection
/// expression has no way to supply cursor boundaries or navigation flags. Prefer the explicit
/// constructor whenever real cursor metadata is available (e.g. when returning a page from a
/// repository/service) - the collection-expression path is intended for tests, fixtures, and
/// quick in-memory construction, not for production paging responses.
/// </remarks>
[DebuggerDisplay("Count = {Count}, {Metadata}")]
[StructLayout(LayoutKind.Auto)]
[JsonConverter(typeof(CursorResultJsonConverterFactory))]
[CollectionBuilder(typeof(CursorResult), nameof(CursorResult.Create))]
public readonly record struct CursorResult<T> :
    IEquatable<CursorResult<T>>,
    IEqualityOperators<CursorResult<T>, CursorResult<T>, bool>,
    IReadOnlyList<T> {

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
    /// Gets the item at the specified index within the current page window.
    /// </summary>
    /// <param name="index">The zero-based index of the item to retrieve.</param>
    /// <exception cref="IndexOutOfRangeException">
    /// <paramref name="index"/> is negative or greater than or equal to <see cref="Count"/>.
    /// Delegates directly to the underlying <see cref="EquatableArray{T}"/> (backed by
    /// <see cref="System.Collections.Immutable.ImmutableArray{T}"/>), which does not perform its
    /// own bounds check and therefore surfaces the array's native <see cref="IndexOutOfRangeException"/>
    /// rather than <see cref="ArgumentOutOfRangeException"/>.
    /// </exception>
    public T this[int index] => this.Items[index];

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
            return [];
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

    /// <summary>
    /// Returns an enumerator that iterates through the current page window.
    /// </summary>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)this.Items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
}

/// <summary>
/// Provides the collection-builder factory used by the compiler to support collection expressions
/// (e.g. <c>[]</c>, <c>[1, 2, 3]</c>) for <see cref="CursorResult{T}"/>.
/// </summary>
public static class CursorResult {

    /// <summary>
    /// Creates a <see cref="CursorResult{T}"/> from a span of items with <see cref="CursorMetadata.Empty"/>.
    /// Invoked by the compiler for collection-expression syntax; not intended to be called directly
    /// when real cursor metadata is available.
    /// </summary>
    /// <typeparam name="T">The type of elements in the paginated collection.</typeparam>
    /// <param name="items">The items to wrap.</param>
    public static CursorResult<T> Create<T>(ReadOnlySpan<T> items) {
        if(items.IsEmpty) {
            return CursorResult<T>.Empty;
        }

        return new CursorResult<T>(items.ToArray(), CursorMetadata.Empty);
    }
}