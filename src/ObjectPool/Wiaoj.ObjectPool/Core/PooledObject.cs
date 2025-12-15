using Wiaoj.ObjectPool.Abstractions;
using Wiaoj.ObjectPool.Internal;

namespace Wiaoj.ObjectPool;
/// <summary>
/// A disposable struct that wraps a leased object from an <see cref="IObjectPool{T}"/>,
/// ensuring it is returned to the pool when disposed. This type is safe to use in
/// both synchronous and asynchronous contexts.
/// </summary>
/// <remarks>
/// This struct is a lightweight handle to a heap-allocated lease state. It can be copied freely,
/// and all copies will point to the same underlying lease. The leased object will be returned
/// to the pool exactly once when the first copy's <see cref="Dispose"/> method is called.
/// </remarks>
/// <typeparam name="T">The type of the object being pooled.</typeparam>
public readonly struct PooledObject<T> : IDisposable, IAsyncDisposable where T : class {
    private readonly Lease<T>? _lease;

    internal PooledObject(T item, IObjectPool<T> pool) {
        this._lease = new Lease<T>(item, pool);
    }

    /// <summary>
    /// Gets the underlying pooled object instance.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this property is accessed after the object has been returned to the pool.
    /// </exception>
    public T Item =>
            this._lease is null
                ? throw new ObjectDisposedException(nameof(PooledObject<>), "This PooledObject is uninitialized.")
                : this._lease.Item;

    /// <summary>
    /// Returns the wrapped object to its pool. This operation is thread-safe and idempotent.
    /// </summary>
    public void Dispose() {
        this._lease?.ReturnToPool();
    }

    /// <summary>
    /// Returns the object to the pool asynchronously.
    /// Allows using the 'await using' syntax.
    /// </summary>
    public ValueTask DisposeAsync() {
        this.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Allows for implicit conversion from a <see cref="PooledObject{T}"/> to the wrapped object of type <typeparamref name="T"/>.
    /// </summary>
    public static implicit operator T(PooledObject<T> pooledObject) {
        return pooledObject.Item;
    }
}