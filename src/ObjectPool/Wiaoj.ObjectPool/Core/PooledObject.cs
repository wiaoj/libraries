using System.Runtime.CompilerServices;
using Wiaoj.ObjectPool.Internal;

namespace Wiaoj.ObjectPool;

/// <summary>
/// A zero-allocation disposable struct managing a leased object from an object pool.
/// </summary>
public readonly struct PooledObject<T> : IDisposable, IAsyncDisposable where T : class {
    private readonly Lease<T>? _lease;

    // Senkron havuzlar için constructor
    internal PooledObject(T item, IObjectPool<T> pool) {
        this._lease = new Lease<T>(item, pool);
    }

    // Asenkron havuzlar (FifoAsyncObjectPool / BoundedAsyncObjectPool) için constructor
    internal PooledObject(T item, IAsyncObjectPool<T> asyncPool) {
        this._lease = new Lease<T>(item, asyncPool);
    }

    /// <summary>
    /// Gets the underlying pooled object instance.
    /// </summary>
    public T Item => this._lease is not null
        ? this._lease.Item
        : throw new ObjectDisposedException(nameof(PooledObject<T>), "Object has already been returned to pool.");

    /// <summary>
    /// Returns the object to the pool safely and idempotently.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() {
        this._lease?.ReturnToPool();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() {
        Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Implicitly converts a <see cref="PooledObject{T}"/> to the wrapped instance.
    /// </summary>
    public static implicit operator T(PooledObject<T> pooledObject) => pooledObject.Item;
}