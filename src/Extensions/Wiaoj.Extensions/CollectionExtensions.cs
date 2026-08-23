using System.Runtime.InteropServices;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.Extensions;

/// <summary>
/// Provides extension methods for <see cref="ICollection{T}"/> and custom buffer primitives.
/// </summary>
public static class CollectionExtensions {
    #region AddRange

    /// <summary>
    /// Adds the elements of the specified collection to the target <see cref="ICollection{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="target">The collection to which items will be added.</param>
    /// <param name="source">The sequence of items to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    public static void AddRange<T>(this ICollection<T> target, IEnumerable<T> source) {
        Preca.ThrowIfNull(target);
        Preca.ThrowIfNull(source);

        if(target is List<T> list) {
            if(source.TryGetNonEnumeratedCount(out int count)) {
                list.Capacity = Math.Max(list.Capacity, list.Count + count);
            }
            list.AddRange(source);
            return;
        }

        foreach(T item in source) {
            target.Add(item);
        }
    }

    /// <summary>
    /// Adds elements from a <see cref="ReadOnlySpan{T}"/> directly to the target collection without intermediate heap allocations.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="target">The target collection.</param>
    /// <param name="source">The read-only span of elements to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is <see langword="null"/>.</exception>
    public static void AddRange<T>(this ICollection<T> target, ReadOnlySpan<T> source) {
        Preca.ThrowIfNull(target);
        if(source.IsEmpty) return;

        if(target is List<T> list) {
            list.Capacity = Math.Max(list.Capacity, list.Count + source.Length);
            foreach(ref readonly T item in source) {
                list.Add(item);
            }
            return;
        }

        foreach(ref readonly T item in source) {
            target.Add(item);
        }
    }

    /// <summary>
    /// Adds multiple collections to the target collection in a single operation, pre-calculating total capacity when possible.
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection.</typeparam>
    /// <param name="target">The target collection.</param>
    /// <param name="collections">A variable-length span or array of collections to append.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is <see langword="null"/>.</exception>
    public static void AddRange<T>(this ICollection<T> target, params ReadOnlySpan<IEnumerable<T>> collections) {
        Preca.ThrowIfNull(target);
        if(collections.IsEmpty) return;

        if(target is List<T> list) {
            int additionalCount = 0;
            foreach(IEnumerable<T>? c in collections) {
                if(c is not null && c.TryGetNonEnumeratedCount(out int count)) {
                    additionalCount += count;
                }
            }

            if(additionalCount > 0) {
                list.Capacity = Math.Max(list.Capacity, list.Count + additionalCount);
            }

            foreach(IEnumerable<T>? c in collections) {
                if(c is null) continue;
                list.AddRange(c);
            }
        }
        else {
            foreach(IEnumerable<T>? c in collections) {
                if(c is null) continue;

                foreach(T item in c) {
                    target.Add(item);
                }
            }
        }
    }

    #endregion

    #region ValueList Interop

    /// <summary>
    /// Copies all elements from an <see cref="IEnumerable{T}"/> into a destination <see cref="ValueList{T}"/> buffer.
    /// </summary>
    /// <typeparam name="T">The type of elements.</typeparam>
    /// <param name="source">The source enumerable.</param>
    /// <param name="target">The target value list reference.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is <see langword="null"/>.</exception>
    public static void CopyToValueList<T>(this IEnumerable<T> source, ref ValueList<T> target) {
        Preca.ThrowIfNull(source);

        // Fast-path: Array
        if(source is T[] array) {
            for(int i = 0; i < array.Length; i++) {
                target.Add(array[i]);
            }
            return;
        }

        // Fast-path: List
        if(source is List<T> list) {
            ReadOnlySpan<T> span = CollectionsMarshal.AsSpan(list);
            for(int i = 0; i < span.Length; i++) {
                target.Add(span[i]);
            }
            return;
        }

        // Fallback: General enumeration
        foreach(T item in source) {
            target.Add(item);
        }
    }

    #endregion
}