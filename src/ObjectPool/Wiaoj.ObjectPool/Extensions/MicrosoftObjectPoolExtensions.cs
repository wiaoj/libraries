using Microsoft.Extensions.ObjectPool;
using Wiaoj.ObjectPool.Internal;
using Wiaoj.Preconditions;

namespace Wiaoj.ObjectPool;

public static class MicrosoftObjectPoolExtensions {
    public static PooledObject<T> Lease<T>(this ObjectPool<T> pool) where T : class {
        Preca.ThrowIfNull(pool);
        T instance = pool.Get();
        return new PooledObject<T>(instance, new DefaultObjectPoolAdapter<T>(pool));
    }
}