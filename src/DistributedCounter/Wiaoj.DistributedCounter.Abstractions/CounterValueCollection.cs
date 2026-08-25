using System.Collections;
using Wiaoj.Primitives;

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
/// Because structs are copied by value, the disposal state is tracked by a shared reference-typed 
/// <see cref="DisposeGuard"/> utilizing <see cref="DisposeState"/>. This ensures that even if multiple copies 
/// of this struct are created, passed around, or disposed concurrently, the underlying pooled resources 
/// are released back to the pool exactly once, and all copies immediately observe the disposed state.
/// </para>
/// <para>
/// After disposal, all read operations (<see cref="Count"/>, indexer, <see cref="TryGetValue"/>, <see cref="GetEnumerator"/>) 
/// return empty/zero values safely without throwing exceptions.
/// </para>
/// </remarks>
public readonly struct CounterValueCollection : IDisposable, IEnumerable<KeyValuePair<string, CounterValue>> {
    private readonly Dictionary<string, CounterValue>? _data;
    private readonly DisposeGuard _guard;

    /// <summary>
    /// Initializes a new instance of the <see cref="CounterValueCollection"/> struct.
    /// </summary>
    /// <param name="data">The dictionary containing counter keys and values, typically leased from an object pool.</param>
    /// <param name="releaser">An optional <see cref="IDisposable"/> instance (e.g. a pooled object lease) responsible for recycling resources.</param>
    public CounterValueCollection(Dictionary<string, CounterValue>? data, IDisposable? releaser) {
        this._data = data;
        this._guard = new DisposeGuard(releaser);
    }

    /// <summary>
    /// Gets a value indicating whether this collection has been disposed or is currently disposing.
    /// </summary>
    private bool IsDisposed => this._guard.IsDisposed;

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
            if(this.IsDisposed) return CounterValue.Zero;
            if(this._data != null && this._data.TryGetValue(key, out CounterValue val)) return val;
            return CounterValue.Zero;
        }
    }

    /// <summary>
    /// Gets the total number of counter values contained in the collection.
    /// </summary>
    /// <value>The count of elements, or <c>0</c> if this instance is disposed or empty.</value>
    public int Count => this.IsDisposed ? 0 : this._data?.Count ?? 0;

    /// <summary>
    /// Determines whether the collection contains a counter value with the specified key.
    /// </summary>
    /// <param name="key">The counter key to locate in the collection.</param>
    /// <returns>
    /// <see langword="true"/> if the collection contains an element with the specified key and is not disposed; 
    /// otherwise, <see langword="false"/>.
    /// </returns>
    public bool ContainsKey(string key) {
        return !this.IsDisposed && (this._data?.ContainsKey(key) ?? false);
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
        if(!this.IsDisposed && this._data != null) return this._data.TryGetValue(key, out value);
        value = CounterValue.Zero;
        return false;
    }

    /// <summary>
    /// Releases all resources held by the collection and returns pooled objects to their pool.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and thread-safe. Calling it multiple times or across different copies 
    /// of this struct will trigger resource disposal exactly once.
    /// </remarks>
    public void Dispose() {
        this._guard.Dispose();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection of counter key-value pairs.
    /// </summary>
    /// <returns>An enumerator for the collection, or an empty enumerator if disposed.</returns>
    public IEnumerator<KeyValuePair<string, CounterValue>> GetEnumerator() {
        if(this.IsDisposed || this._data is null) return Enumerable.Empty<KeyValuePair<string, CounterValue>>().GetEnumerator();
        return this._data.GetEnumerator();
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }

    /// <summary>
    /// Internal thread-safe, lock-free dispose guard shared across all copies of the owning struct.
    /// Uses <see cref="DisposeState"/> to coordinate binary state transitions and prevent double-release.
    /// </summary>
    private sealed class DisposeGuard(IDisposable? inner) : IDisposable {
        private readonly DisposeState _state = new();

        /// <summary>
        /// Gets a value indicating whether disposal has been initiated or completed.
        /// </summary>
        public bool IsDisposed => this._state.IsDisposingOrDisposed;

        /// <summary>
        /// Atomically disposes the underlying resource at most once.
        /// </summary>
        public void Dispose() {
            if(this._state.TryBeginDispose()) {
                try {
                    inner?.Dispose();
                }
                finally {
                    this._state.SetDisposed();
                }
            }
        }
    }
}