using System.Collections;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// A high-performance, read-only collection wrapper for batch-queried counter values.
/// Returns resources to the underlying object pool upon disposal.
/// </summary>
/// <remarks>
/// <para>
/// This is a <see langword="readonly struct"/> to avoid heap allocations on batch retrieval paths such as 
/// <see cref="IDistributedCounterService.GetValuesAsync"/>.
/// </para>
/// <para>
/// After disposal, all read operations (<see cref="Count"/>, indexer, <see cref="TryGetValue"/>, <see cref="GetEnumerator"/>) 
/// return empty/zero values safely without throwing exceptions.
/// </para>
/// </remarks>
public readonly struct CounterValueCollection : IEnumerable<KeyValuePair<string, CounterValue>> {
    private readonly Dictionary<string, CounterValue>? _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterValueCollection"/> struct.
    /// </summary>
    /// <param name="data">The dictionary containing counter keys and values, typically leased from an object pool.</param>
    public CounterValueCollection(Dictionary<string, CounterValue>? data) {
        this._data = data;
    }

    /// <summary>
    /// Gets the <see cref="CounterValue"/> associated with the specified counter key.
    /// </summary>
    /// <param name="key">The counter name/key to look up.</param>
    /// <returns>
    /// The associated <see cref="CounterValue"/> if found; otherwise, <see cref="CounterValue.Zero"/>.
    /// Always returns <see cref="CounterValue.Zero"/> if this collection has already been disposed.
    /// </returns>
    public CounterValue this[string key] {
        get {
            if(this._data != null && this._data.TryGetValue(key, out CounterValue val)) return val;
            return CounterValue.Zero;
        }
    }

    /// <summary>
    /// Gets the total number of counter values contained in the collection.
    /// </summary>
    /// <value>The count of elements, or <c>0</c> if this instance is disposed or empty.</value>
    public int Count => this._data?.Count ?? 0;

    /// <summary>
    /// Determines whether the collection contains a counter value with the specified key.
    /// </summary>
    /// <param name="key">The counter key to locate in the collection.</param>
    /// <returns>
    /// <see langword="true"/> if the collection contains an element with the specified key and is not disposed; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ContainsKey(string key) {
        return this._data?.ContainsKey(key) ?? false;
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    /// <param name="key">The counter key to locate.</param>
    /// <param name="value">
    /// When this method returns, contains the <see cref="CounterValue"/> associated with the specified key, 
    /// if the key is found and the instance is not disposed; otherwise, <see cref="CounterValue.Zero"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key was found and the collection is active; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(string key, out CounterValue value) {
        if(this._data != null) return this._data.TryGetValue(key, out value);
        value = CounterValue.Zero;
        return false;
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of counter key-value pairs.
    /// </summary>
    /// <returns>An enumerator for the collection, or an empty enumerator if disposed.</returns>
    public IEnumerator<KeyValuePair<string, CounterValue>> GetEnumerator() {
        if(this._data is null) return Enumerable.Empty<KeyValuePair<string, CounterValue>>().GetEnumerator();
        return this._data.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}