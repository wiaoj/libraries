namespace Wiaoj.ObjectPool;

/// <summary>
/// Defines a contract for asynchronously creating and resetting objects within a pool.
/// This is essential for pooling objects whose creation or reset involves I/O operations.
/// </summary>
/// <typeparam name="T">The type of object to pool.</typeparam>
public interface IAsyncPoolPolicy<T> where T : notnull {
    /// <summary>
    /// Asynchronously creates a new instance of the object.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous creation operation, resulting in a new instance of <typeparamref name="T"/>.</returns>
    ValueTask<T> CreateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Asynchronously resets an object to its default state, preparing it for reuse.
    /// </summary>
    /// <param name="obj">The object to reset.</param>
    /// <returns>
    /// A task representing the reset operation. The result is <see langword="true"/> if the object was successfully reset and can be returned to the pool;
    /// otherwise, <see langword="false"/> to indicate the object should be discarded.
    /// </returns>
    ValueTask<bool> TryResetAsync(T obj);
}