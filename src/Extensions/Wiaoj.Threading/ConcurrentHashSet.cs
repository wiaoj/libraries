using System.Collections;
using System.Collections.Concurrent;

namespace Wiaoj.Concurrency;
/// <summary>
/// Represents a thread-safe, unordered collection of unique elements.
/// </summary>
/// <remarks>
/// This class provides a high-performance, thread-safe set implementation that is missing from the Base Class Library.
/// It is useful for scenarios like deduplication, tracking processed items, and managing unique resources in a concurrent environment.
/// </remarks>
/// <typeparam name="T">The type of elements in the hash set.</typeparam>
public class ConcurrentHashSet<T> : IEnumerable<T> where T : notnull {
    private readonly ConcurrentDictionary<T, byte> _dictionary;

    // --- Constructors ---
    public ConcurrentHashSet() {
        this._dictionary = new ConcurrentDictionary<T, byte>();
    }

    public ConcurrentHashSet(IEqualityComparer<T> comparer) {
        this._dictionary = new ConcurrentDictionary<T, byte>(comparer);
    }

    // --- Core Properties ---
    public int Count => this._dictionary.Count;
    public bool IsEmpty => this._dictionary.IsEmpty;

    // --- Core Methods ---

    /// <summary>
    /// Adds the specified element to the set.
    /// </summary>
    /// <returns>true if the element is added to the set; false if the element is already in the set.</returns>
    public bool Add(T item) {
        return this._dictionary.TryAdd(item, 0);
    }

    /// <summary>
    /// Determines whether the set contains a specific element.
    /// </summary>
    public bool Contains(T item) {
        return this._dictionary.ContainsKey(item);
    }

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <returns>true if the element is successfully found and removed; otherwise, false.</returns>
    public bool Remove(T item) {
        return this._dictionary.TryRemove(item, out _);
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    public void Clear() {
        this._dictionary.Clear();
    }

    // --- IEnumerable Implementation ---
    public IEnumerator<T> GetEnumerator() {
        return this._dictionary.Keys.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}