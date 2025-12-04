using Microsoft.Extensions.ObjectPool;

namespace Wiaoj.ObjectPool.Internal;

internal sealed class DefaultObjectPoolAdapter<T>(
    ObjectPool<T> underlyingPool

#pragma warning disable CS9113 // Parameter is unread.
    , ObjectPoolOptions options
#pragma warning restore CS9113 // Parameter is unread.

    ) : IObjectPool<T> where T : class {
#if DEBUG 
    private readonly object _syncRoot = new();
#endif

    /// <inheritdoc/>
    public T Get() {
#if DEBUG 
        lock (this._syncRoot) {
            T instance = underlyingPool.Get();

            if (options.LeakDetectionEnabled)
                LeakDetector.Track(instance);

            return instance;
        }
#else 
        return underlyingPool.Get();
#endif
    }

    /// <inheritdoc/>
    public PooledObject<T> Lease() {
        T instance = Get();

        return new PooledObject<T>(instance, this);
    }

    /// <inheritdoc/>
    public void Return(T @object) {
#if DEBUG
        lock (this._syncRoot) {
            underlyingPool.Return(@object);

            if (options.OnReturnValidation is not null) {
                try {
                    options.OnReturnValidation(@object);
                }
                catch (Exception ex) {
                    throw new InvalidOperationException(
                        $"An object of type '{typeof(T).Name}' failed validation upon being returned to the pool. " +
                        "This indicates a bug in its reset logic (e.g., in `TryReset` or the lambda resetter). " +
                        "See inner exception for details.", ex);
                }
            }

            if (options.LeakDetectionEnabled)
                LeakDetector.Untrack(@object);
        }
#else
        underlyingPool.Return(@object);
#endif
    }
}