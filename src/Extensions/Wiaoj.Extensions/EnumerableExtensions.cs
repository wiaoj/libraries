using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Wiaoj.Extensions;

/// <summary>
/// Provides high-performance, allocation-aware extension methods for <see cref="IEnumerable{T}"/>.
/// </summary>
public static class EnumerableExtensions {
    #region State Checks

    /// <summary>
    /// Indicates whether the specified enumerable is <see langword="null"/> or contains no elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="source">The sequence to test for null or emptiness.</param>
    /// <returns>
    /// <see langword="true"/> if the <paramref name="source"/> sequence is <see langword="null"/> or contains no elements;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    /// <remarks>
    /// Uses <see cref="Enumerable.TryGetNonEnumeratedCount{TSource}"/> first to evaluate count in <c>O(1)</c> time
    /// without allocating an enumerator for recognized collections before falling back to <see cref="Enumerable.Any{T}(IEnumerable{T})"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this IEnumerable<T>? source) {
        if(source is null) return true;

        if(source.TryGetNonEnumeratedCount(out int count)) {
            return count == 0;
        }

        return !source.Any();
    }

    /// <summary>
    /// Indicates whether the specified enumerable is not <see langword="null"/> and contains at least one element.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the sequence.</typeparam>
    /// <param name="source">The sequence to test.</param>
    /// <returns>
    /// <see langword="true"/> if the <paramref name="source"/> sequence is not <see langword="null"/> and contains at least one element;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasItems<T>([NotNullWhen(true)] this IEnumerable<T>? source) {
        return !source.IsNullOrEmpty();
    }

    #endregion

    #region Null Filtering

    /// <summary>
    /// Filters out <see langword="null"/> elements from a sequence of reference types.
    /// </summary>
    /// <typeparam name="T">The reference type of the elements.</typeparam>
    /// <param name="source">The sequence containing potentially null elements.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing non-null elements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : class {
        Preca.ThrowIfNull(source);
        return Iterator(source);

        static IEnumerable<T> Iterator(IEnumerable<T?> src) {
            foreach(T? item in src) {
                if(item is not null) {
                    yield return item;
                }
            }
        }
    }

    /// <summary>
    /// Filters out <see langword="null"/> values from a sequence of nullable value types.
    /// </summary>
    /// <typeparam name="T">The underlying value type.</typeparam>
    /// <param name="source">The sequence containing potentially null nullable structs.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> of unwrapped non-null values.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source) where T : struct {
        Preca.ThrowIfNull(source);
        return Iterator(source);

        static IEnumerable<T> Iterator(IEnumerable<T?> src) {
            foreach(T? item in src) {
                if(item.HasValue) {
                    yield return item.GetValueOrDefault();
                }
            }
        }
    }

    #endregion

    #region Safe Element Extraction

    /// <summary>
    /// Attempts to retrieve the first element of the sequence without throwing an exception.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="value">When this method returns, contains the first element if found; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if the sequence contained at least one element; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is <see langword="null"/>.</exception>
    public static bool TryGetFirst<T>(this IEnumerable<T> source, [MaybeNullWhen(false)] out T value) {
        Preca.ThrowIfNull(source);

        if(source is IReadOnlyList<T> list) {
            if(list.Count > 0) {
                value = list[0];
                return true;
            }
            value = default;
            return false;
        }

        using IEnumerator<T> enumerator = source.GetEnumerator();
        if(enumerator.MoveNext()) {
            value = enumerator.Current;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to retrieve the first element of the sequence that matches a specified condition.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="source">The source sequence.</param>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="value">When this method returns, contains the first matching element if found; otherwise, the default value.</param>
    /// <returns><see langword="true"/> if a matching element was found; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> or <paramref name="predicate"/> is <see langword="null"/>.</exception>
    public static bool TryGetFirst<T>(this IEnumerable<T> source, Func<T, bool> predicate, [MaybeNullWhen(false)] out T value) {
        Preca.ThrowIfNull(source);
        Preca.ThrowIfNull(predicate);

        foreach(T item in source) {
            if(predicate(item)) {
                value = item;
                return true;
            }
        }

        value = default;
        return false;
    }

    #endregion
}