using Wiaoj.Preconditions;

namespace Wiaoj.ObjectPool.Testing;

/// <summary>
/// A full-featured, thread-safe in-memory test double for <see cref="IObjectPool{T}"/>.
/// Specifically designed for unit and integration testing with built-in leak detection and tracking capabilities.
/// </summary>
/// <typeparam name="T">The type of object managed by the pool.</typeparam>
public sealed class FakeObjectPool<T> : IObjectPool<T> where T : class {
    private readonly Func<T> _factory;
    private readonly Action<T>? _resetter;

    private int _totalLeased;
    private int _totalReturned;

    /// <summary>
    /// Gets the total number of lease operations performed so far.
    /// </summary>
    public int TotalLeased => Volatile.Read(ref this._totalLeased);

    /// <summary>
    /// Gets the total number of objects returned to the pool.
    /// </summary>
    public int TotalReturned => Volatile.Read(ref this._totalReturned);

    /// <summary>
    /// Gets the current number of active leases (leased but not yet returned/disposed).
    /// Ideal for asserting zero memory leaks (<c>Assert.Equal(0, pool.ActiveLeases)</c>).
    /// </summary>
    public int ActiveLeases => TotalLeased - TotalReturned;

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeObjectPool{T}"/> class using a factory delegate.
    /// </summary>
    /// <param name="factory">The factory function that instantiates objects on demand.</param>
    /// <param name="resetter">An optional resetter callback executed when an object is returned to the pool.</param>
    public FakeObjectPool(Func<T> factory, Action<T>? resetter = null) {
        Preca.ThrowIfNull(factory);
        this._factory = factory;
        this._resetter = resetter;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FakeObjectPool{T}"/> class for types with a parameterless constructor.
    /// </summary>
    public FakeObjectPool(Action<T>? resetter = null)
        : this(() => Activator.CreateInstance<T>(), resetter) { }

    /// <inheritdoc/>
    public PooledObject<T> Lease() {
        Interlocked.Increment(ref this._totalLeased);
        T item = this._factory();
        return new PooledObject<T>(item, this);
    }

    /// <inheritdoc/>
    public T Get() {
        Interlocked.Increment(ref this._totalLeased);
        return this._factory();
    }

    /// <inheritdoc/>
    public void Return(T obj) {
        Interlocked.Increment(ref this._totalReturned);
        this._resetter?.Invoke(obj);
    }
}