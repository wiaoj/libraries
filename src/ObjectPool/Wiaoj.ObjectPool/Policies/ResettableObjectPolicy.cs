namespace Wiaoj.ObjectPool.Policies;
/// <summary>
/// A unified policy for objects that implement the IResettable interface.
/// supports both Synchronous and Asynchronous pooling scenarios.
/// </summary>
/// <typeparam name="T">The type of object to pool, which must implement IResettable and have a parameterless constructor.</typeparam>
internal sealed class ResettableObjectPolicy<T> : IPoolPolicy<T>, IAsyncPoolPolicy<T> where T : class, IResettable, new() {

    // --- Synchronous Implementation ---
    public T Create() {
        return new T();
    }

    public bool TryReset(T obj) {
        return obj.TryReset();
    }

    // --- Asynchronous Implementation ---
    // Since the object creation (new T()) and reset (TryReset()) are synchronous operations in IResettable,
    // we wrap them in ValueTask for async compatibility. This allows using simple IResettable objects
    // in high-performance Async Pools without needing a separate policy.

    public ValueTask<T> CreateAsync(CancellationToken cancellationToken) {
        return new ValueTask<T>(new T());
    }

    public ValueTask<bool> TryResetAsync(T obj) {
        return new ValueTask<bool>(obj.TryReset());
    }
}