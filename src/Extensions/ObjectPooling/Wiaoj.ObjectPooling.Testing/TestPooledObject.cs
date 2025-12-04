using Wiaoj.ObjectPool;

namespace Wiaoj.ObjectPooling.Testing;
/// <summary>
/// Provides a factory method for creating valid <see cref="PooledObject{T}"/> instances for use in test environments.
/// This is necessary to bypass the internal constructor of <see cref="PooledObject{T}"/>.
/// </summary>
public static class TestPooledObject {
    /// <summary>
    /// Creates a valid <see cref="PooledObject{T}"/> that wraps the provided item.
    /// The returned object can be safely disposed without causing errors in a test environment.
    /// </summary>
    /// <typeparam name="T">The type of the object to wrap.</typeparam>
    /// <param name="item">The object instance to be wrapped by the <see cref="PooledObject{T}"/>.</param>
    /// <returns>A new instance of <see cref="PooledObject{T}"/> suitable for testing.</returns>
    public static PooledObject<T> CreateForTesting<T>(T item) where T : class {
        NoOpObjectPool<T> noOpPool = new();
        return new PooledObject<T>(item, noOpPool);
    }
}