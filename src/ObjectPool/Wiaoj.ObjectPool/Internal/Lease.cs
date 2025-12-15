using Wiaoj.Primitives;

namespace Wiaoj.ObjectPool.Internal;
/// <summary>
/// A private, heap-allocated class that holds the state for a single leased object.
/// This allows the <see cref="PooledObject{T}"/> struct to be safely copied across await boundaries
/// while ensuring the underlying resource is disposed exactly once.
/// </summary>
/// <typeparam name="T">The type of object being leased.</typeparam>
internal sealed class Lease<T> where T : class {
    private readonly T _item;
    private readonly IObjectPool<T> _pool;

    private readonly DisposeState _disposeState;

    /// <summary>
    /// Initializes a new instance of the <see cref="Lease{T}"/> class.
    /// </summary>
    internal Lease(T item, IObjectPool<T> pool) {
        this._item = item;
        this._pool = pool;
        this._disposeState = new DisposeState();
    }

    /// <summary>
    /// Gets the underlying pooled object.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if the object is accessed after it has been returned to the pool.
    /// </exception>
    public T Item {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(PooledObject<>));
            return this._item;
        }
    }

    /// <summary>
    /// Attempts to return the leased object to the pool.
    /// This operation is thread-safe and idempotent.
    /// </summary>
    public void ReturnToPool() {
        if (this._disposeState.TryBeginDispose()) {
            try {
                this._pool.Return(this._item);
            }
            finally {
                this._disposeState.SetDisposed();
            }
        }
    }
}