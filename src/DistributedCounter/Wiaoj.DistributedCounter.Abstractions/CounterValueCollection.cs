using System.Collections;

namespace Wiaoj.DistributedCounter;

/// <summary>
/// A read-only collection of counter values.
/// Must be disposed to return resources to the underlying pool.
/// </summary>
/// <remarks>
/// This is a <see langword="readonly struct"/> to avoid a heap allocation for the wrapper on the
/// <see cref="IDistributedCounterService.GetValuesAsync"/> return path. Because structs copy by
/// value, disposal state is intentionally kept in a small reference-typed <see cref="DisposeGuard"/>
/// shared across every copy — this guarantees the pooled dictionary is returned to the pool
/// exactly once no matter how many copies of this struct exist or how many of them get disposed,
/// and it prevents a disposed copy from reading a dictionary that the pool has since re-leased to
/// an unrelated caller.
/// </remarks>
public readonly struct CounterValueCollection : IDisposable, IEnumerable<KeyValuePair<string, CounterValue>> {
    private readonly Dictionary<string, CounterValue>? _data;
    private readonly DisposeGuard? _guard;

    /// <summary>
    /// Initializes a new instance of <see cref="CounterValueCollection"/>.
    /// </summary>
    /// <param name="data">The dictionary containing counter keys and values.</param>
    /// <param name="releaser">An optional object to handle resource cleanup.</param>
    public CounterValueCollection(Dictionary<string, CounterValue> data, IDisposable? releaser) {
        this._data = data;
        this._guard = releaser is null ? null : new DisposeGuard(releaser);
    }

    private bool IsDisposed => this._guard?.IsDisposed ?? false;

    /// <summary>
    /// Gets the <see cref="CounterValue"/> associated with the specified key.
    /// Returns <see cref="CounterValue.Zero"/> if the key is not found, or if this instance
    /// has already been disposed.
    /// </summary>
    public CounterValue this[string key] {
        get {
            if(this.IsDisposed) return CounterValue.Zero;
            if(this._data != null && this._data.TryGetValue(key, out CounterValue val)) return val;
            return CounterValue.Zero;
        }
    }

    /// <summary>
    /// Gets the number of elements in the collection. Returns <c>0</c> if this instance has
    /// already been disposed.
    /// </summary>
    public int Count => this.IsDisposed ? 0 : this._data?.Count ?? 0;

    /// <summary>
    /// Determines whether the collection contains a specific counter key.
    /// Returns <see langword="false"/> if this instance has already been disposed.
    /// </summary>
    public bool ContainsKey(string key) {
        return !this.IsDisposed && (this._data?.ContainsKey(key) ?? false);
    }

    /// <summary>
    /// Attempts to get the value associated with the specified key.
    /// Returns <see langword="false"/> if this instance has already been disposed.
    /// </summary>
    public bool TryGetValue(string key, out CounterValue value) {
        if(!this.IsDisposed && this._data != null) return this._data.TryGetValue(key, out value);
        value = CounterValue.Zero;
        return false;
    }

    /// <summary>
    /// Releases the resources used by the collection. Safe to call multiple times, and safe to
    /// call on multiple copies of this struct — the underlying resource is released exactly once.
    /// </summary>
    public void Dispose() {
        this._guard?.Dispose();
    }

    public IEnumerator<KeyValuePair<string, CounterValue>> GetEnumerator() {
        if(this.IsDisposed || this._data is null) return Enumerable.Empty<KeyValuePair<string, CounterValue>>().GetEnumerator();
        return this._data.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Reference-typed, idempotent dispose guard shared across every copy of the owning
    /// <see cref="CounterValueCollection"/> struct. <see cref="Interlocked.Exchange(ref int, int)"/>
    /// ensures <see cref="IDisposable.Dispose"/> on the wrapped releaser runs at most once even
    /// under concurrent calls from different copies.
    /// </summary>
    private sealed class DisposeGuard(IDisposable inner) : IDisposable {
        private int _disposed;

        public bool IsDisposed => Volatile.Read(ref this._disposed) != 0;

        public void Dispose() {
            if(Interlocked.Exchange(ref this._disposed, 1) == 0) {
                inner.Dispose();
            }
        }
    }
}
