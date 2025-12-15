using Microsoft.Extensions.ObjectPool;
using Wiaoj.ObjectPool.Abstractions;
using Wiaoj.ObjectPool.Internal;
using Wiaoj.ObjectPool.Internal.AsyncObjectPool;

namespace Wiaoj.ObjectPool;
/// <summary>
/// Provides factory methods for creating <see cref="IObjectPool{T}"/> instances.
/// </summary>
public static class ObjectPoolFactory {
    /// <summary>
    /// Creates a new object pool for the specified type using the given pool policy and optional configuration.
    /// This method can be used to create object pools without relying on dependency injection.
    /// </summary>
    /// <typeparam name="T">The type of objects to be pooled. Must be a reference type.</typeparam>
    /// <param name="policy">The policy that defines how objects are created, returned, and reset in the pool.</param>
    /// <param name="options">Optional configuration settings for the pool. If not provided, default options are used.</param>
    /// <returns>An <see cref="IObjectPool{T}"/> instance configured with the specified policy and options.</returns>
    public static IObjectPool<T> Create<T>(IPoolPolicy<T> policy, ObjectPoolOptions? options = null) where T : class {
        options ??= new ObjectPoolOptions();

        DefaultObjectPoolProvider provider = new() {
            MaximumRetained = options.MaximumRetained
        };

        MicrosoftPooledObjectPolicyAdapter<T> microsoftPolicy = new(policy);
        ObjectPool<T> microsoftPool = provider.Create(microsoftPolicy);

        return new DefaultObjectPoolAdapter<T>(microsoftPool);
    }

    /// <summary>
    /// Creates a new asynchronous object pool for the specified type.
    /// </summary>
    public static IAsyncObjectPool<T> CreateAsync<T>(IAsyncPoolPolicy<T> policy, ObjectPoolOptions? options = null) where T : class {
        options ??= new ObjectPoolOptions();

        if (options.AccessMode == PoolAccessMode.Bounded) {
            return new BoundedAsyncObjectPool<T>(policy, options);
        }
        else {
            return new FifoAsyncObjectPool<T>(policy, options);
        }
    }
}