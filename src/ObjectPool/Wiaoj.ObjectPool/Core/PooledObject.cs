using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wiaoj.ObjectPool;

/// <summary>
/// A zero-allocation, disposable struct that wraps a leased object from an object pool.
/// ensuring it is returned to the pool when disposed.
/// </summary>
/// <remarks>
/// IMPORTANT: This is a value type (struct). Do NOT copy this object or pass it by value.
/// It is designed to be used strictly within a 'using' or 'await using' statement.
/// </remarks>
public struct PooledObject<T> : IDisposable, IAsyncDisposable where T : class {
    private T? _item;
     
    private readonly IObjectPool<T>? _syncPool;
    private readonly IAsyncObjectPool<T>? _asyncPool;

    // Senkron havuzlar için internal constructor
    internal PooledObject(T item, IObjectPool<T> pool) {
        _item = item;
        _syncPool = pool;
        _asyncPool = null;
    }

    // Asenkron havuzlar için internal constructor
    internal PooledObject(T item, IAsyncObjectPool<T> pool) {
        _item = item;
        _syncPool = null;
        _asyncPool = pool;
    }

    /// <summary>
    /// Gets the underlying pooled object instance.
    /// </summary>
    public readonly T Item =>
        _item ?? throw new ObjectDisposedException(nameof(PooledObject<>), "This object has already been returned to the pool.");

    /// <summary>
    /// Returns the wrapped object to its pool.
    /// </summary>
    public void Dispose() {
        // Interlocked.Exchange, _item'ı null yapar ve eski değerini döndürür.
        // Bu sayede aynı değişken üzerinde iki kere Dispose() çağrılırsa çökme veya çift iade engellenir.
        T? item = Interlocked.Exchange(ref _item, null);

        if(item is not null) {
            if(_syncPool is not null) {
                _syncPool.Return(item);
            }
            else {
                _asyncPool?.Return(item);
            }
        }
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