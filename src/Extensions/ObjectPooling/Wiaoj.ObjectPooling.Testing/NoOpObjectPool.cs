using Wiaoj.ObjectPool;

namespace Wiaoj.ObjectPooling.Testing;
/// <summary>
/// A "no-operation" implementation of ObjectPool for testing purposes.
/// Its purpose is to satisfy the PooledObject constructor, which requires a non-null pool.
/// </summary>
internal sealed class NoOpObjectPool<T> : IObjectPool<T> where T : class {
    public T Get() {
        throw new NotSupportedException($"Get() should not be called on the {nameof(NoOpObjectPool<T>)}.");
    }

    public PooledObject<T> Lease() {
        throw new NotImplementedException();
    }

    public void Return(T obj) {
        // This method is called when PooledObject.Dispose() is invoked.
        // In a test environment, we don't need to return the object to a real pool,
        // so this method is intentionally left empty.
    }
}