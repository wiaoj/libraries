using Microsoft.Extensions.ObjectPool;

namespace Wiaoj.ObjectPool.Internal; 
internal sealed class DefaultObjectPoolAdapter<T>(ObjectPool<T> underlyingPool) : IObjectPool<T> where T : class { 
    /// <inheritdoc/>
    public T Get() {
        return underlyingPool.Get();
    }

    /// <inheritdoc/>
    public PooledObject<T> Lease() {
        T instance = Get();
        return new PooledObject<T>(instance, this);
    }

    /// <inheritdoc/>
    public void Return(T @object) {
        underlyingPool.Return(@object);
    }
}