using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace Wiaoj.Primitives.Collections;

/// <summary>
/// Provides static factory methods and extension methods to create <see cref="EquatableArray{T}"/> collections.
/// </summary>
public static class EquatableArray {

    /// <summary>
    /// Creates a new <see cref="EquatableArray{T}"/> from the specified <see cref="ReadOnlySpan{T}"/> values.
    /// Modern C# allows this to handle single, multiple, or array inputs with zero allocation.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="values">The read-only span containing the values to add to the new array.</param>
    /// <returns>A new <see cref="EquatableArray{T}"/> instance containing the specified values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> Create<T>(params ReadOnlySpan<T> values) {
        return new EquatableArray<T>(ImmutableArray.Create(values));
    }

    /// <summary>
    /// Creates an empty <see cref="EquatableArray{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <returns>An empty <see cref="EquatableArray{T}"/> instance containing no elements.</returns>[MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> Create<T>() {
        return EquatableArray<T>.Empty;
    }

    // ==============================================================================
    // EXTENSION METHODS (ToEquatableArray)
    // ==============================================================================

    /// <summary>
    /// Enumerates a sequence and produces an <see cref="EquatableArray{T}"/> of its contents.
    /// </summary>
    /// <typeparam name="T">The type of element in the sequence.</typeparam>
    /// <param name="items">The sequence to enumerate.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> containing the specified items.</returns>
    public static EquatableArray<T> ToEquatableArray<T>(this IEnumerable<T>? items) {
        if(items is EquatableArray<T> existing) {
            return existing;
        }

        return new EquatableArray<T>(items);
    }

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from a standard array.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="items">The array of elements.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> containing the items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> ToEquatableArray<T>(this T[]? items) {
        return new EquatableArray<T>(items);
    }

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from an existing <see cref="ImmutableArray{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the array.</typeparam>
    /// <param name="items">The immutable array.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> wrapping the given immutable array.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> ToEquatableArray<T>(this ImmutableArray<T> items) {
        return new EquatableArray<T>(items);
    }

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from a <see cref="ReadOnlySpan{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="items">The read-only span.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> containing the items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> ToEquatableArray<T>(this ReadOnlySpan<T> items) {
        return Create(items);
    }

    /// <summary>
    /// Creates an <see cref="EquatableArray{T}"/> from a <see cref="Span{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the span.</typeparam>
    /// <param name="items">The span.</param>
    /// <returns>An <see cref="EquatableArray{T}"/> containing the items.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EquatableArray<T> ToEquatableArray<T>(this Span<T> items) {
        return Create((ReadOnlySpan<T>)items);
    }
}