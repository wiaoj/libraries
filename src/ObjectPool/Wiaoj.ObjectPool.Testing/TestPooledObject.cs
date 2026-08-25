using Wiaoj.Preconditions;

namespace Wiaoj.ObjectPool.Testing;

/// <summary>
/// Provides helper factory methods for creating valid <see cref="PooledObject{T}"/> instances in test environments.
/// </summary>
public static class TestPooledObject {
    /// <summary>
    /// Creates a valid <see cref="PooledObject{T}"/> wrapping the provided item with a no-op pool.
    /// </summary>
    public static PooledObject<T> CreateForTesting<T>(T item) where T : class {
        Preca.ThrowIfNull(item);
        NoOpObjectPool<T> noOpPool = new(() => item);
        return new PooledObject<T>(item, noOpPool);
    }

    /// <summary>
    /// Creates a valid <see cref="PooledObject{T}"/> that executes a callback when disposed.
    /// </summary>
    public static PooledObject<T> CreateWithCallback<T>(T item, Action<T> onDispose) where T : class {
        Preca.ThrowIfNull(item);
        Preca.ThrowIfNull(onDispose);

        FakeObjectPool<T> pool = new(() => item, onDispose);
        return pool.Lease();
    }
}