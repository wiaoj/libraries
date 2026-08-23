using Wiaoj.Primitives;

namespace Wiaoj.ObjectPool.Internal;

/// <summary>
/// Holds the lifecycle state for a leased object, coordinating safe pool return across sync and async pools.
/// </summary>
internal sealed class Lease<T> where T : class {
    private readonly T _item;
    private readonly IObjectPool<T>? _syncPool;
    private readonly IAsyncObjectPool<T>? _asyncPool;
    private readonly DisposeState _disposeState;

    internal Lease(T item, IObjectPool<T> syncPool) {
        this._item = item;
        this._syncPool = syncPool;
        this._asyncPool = null;
        this._disposeState = new DisposeState();
    }

    internal Lease(T item, IAsyncObjectPool<T> asyncPool) {
        this._item = item;
        this._syncPool = null;
        this._asyncPool = asyncPool;
        this._disposeState = new DisposeState();
    }

    public T Item {
        get {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(PooledObject<>));
            return this._item;
        }
    }

    public void ReturnToPool() {
        if(this._disposeState.TryBeginDispose()) {
            try {
                if(this._syncPool is not null) {
                    this._syncPool.Return(this._item);
                }
                else {
                    this._asyncPool?.Return(this._item);
                }
            }
            finally {
                this._disposeState.SetDisposed();
            }
        }
    }
}