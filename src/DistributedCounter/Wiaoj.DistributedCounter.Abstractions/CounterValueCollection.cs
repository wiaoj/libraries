using System.Collections;

namespace Wiaoj.DistributedCounter;
/// <summary>
/// A read-only collection of counter values. 
/// Must be disposed to return resources to the underlying pool.
/// </summary>
public readonly struct CounterValueCollection : IDisposable, IEnumerable<KeyValuePair<string, CounterValue>> { 
    private readonly Dictionary<string, CounterValue> _data;
    private readonly IDisposable? _releaser;

    /// <summary>
    /// Initializes a new instance of <see cref="CounterValueCollection"/>.
    /// </summary>
    /// <param name="data">The dictionary containing counter keys and values.</param>
    /// <param name="releaser">An optional object to handle resource cleanup.</param>
    public CounterValueCollection(Dictionary<string, CounterValue> data, IDisposable? releaser) {
        this._data = data;
        this._releaser = releaser;
    }

    /// <summary>
    /// Gets the <see cref="CounterValue"/> associated with the specified key.
    /// Returns <see cref="CounterValue.Zero"/> if the key is not found.
    /// </summary>
    public CounterValue this[string key] {
        get {
            if(this._data != null && this._data.TryGetValue(key, out CounterValue val)) return val;
            return CounterValue.Zero;
        }
    }

    /// <summary>
    /// Gets the number of elements in the collection.
    /// </summary>
    public int Count => this._data?.Count ?? 0;

    /// <summary>
    /// Determines whether the collection contains a specific counter key.
    /// </summary>
    public bool ContainsKey(string key) => this._data?.ContainsKey(key) ?? false;

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// </summary>
    public bool TryGetValue(string key, out CounterValue value) {
        if(this._data != null) return this._data.TryGetValue(key, out value);
        value = CounterValue.Zero;
        return false;
    }

    /// <summary>
    /// Releases the resources used by the collection.
    /// </summary>
    public void Dispose() => this._releaser?.Dispose();

    public IEnumerator<KeyValuePair<string, CounterValue>> GetEnumerator() =>
        (this._data ?? Enumerable.Empty<KeyValuePair<string, CounterValue>>()).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}