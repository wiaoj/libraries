namespace Wiaoj.ObjectPool.Internal;

/// <summary>
/// A default pool policy for objects that implement the IResettable interface.
/// </summary>
/// <typeparam name="T">The type of object to pool, which must implement IResettable.</typeparam>
internal sealed class ResettableObjectPolicy<T> : IPoolPolicy<T> where T : class, IResettable, new() {
    public T Create() {
        return new T();
    }

    public bool TryReset(T obj) {
        return obj.TryReset();
    }
}