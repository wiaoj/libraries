namespace Wiaoj.ObjectPool;
/// <summary>
/// Defines a contract for an object pool that supports asynchronous leasing of objects.
/// This is the preferred type to inject for services that require asynchronously created pooled objects.
/// </summary>
/// <typeparam name="T">The type of object being pooled.</typeparam>
public interface IAsyncObjectPool<T> where T : class {
    /// <summary>
    /// Asynchronously leases an object from the pool within a disposable scope.
    /// This is the safest and recommended way to manage the lifetime of a pooled object.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests while waiting for an object.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that completes with a <see cref="PooledObject{T}"/> instance.
    /// </returns>
    ValueTask<PooledObject<T>> LeaseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets an object from the pool whose lifetime must be managed manually.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: An object obtained via this method MUST be returned by calling <see cref="Return"/>.
    /// </remarks>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that results in an object from the pool.</returns>
    ValueTask<T> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns an object to the pool. This operation is non-blocking.
    /// </summary>
    /// <param name="object">The object to return to the pool.</param>
    void Return(T @object);

    PoolStats GetStats();
}

public readonly struct PoolStats(int free, int max) {
    public int Free { get; } = free;
    public int MaxCapacity { get; } = max;
}