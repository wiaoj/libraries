namespace Wiaoj.ObjectPool;
/// <summary>
/// Defines a contract for creating and resetting objects within a pool.
/// This is the core policy interface for the Wiaoj.ObjectPooling library.
/// </summary>
/// <typeparam name="T">The type of object to pool.</typeparam>
public interface IPoolPolicy<T> where T : notnull {
    /// <summary>
    /// Creates a new instance of the object.
    /// </summary>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    T Create();

    /// <summary>
    /// Resets an object to its default state, preparing it for reuse.
    /// </summary>
    /// <param name="obj">The object to reset.</param>
    /// <returns>
    /// <see langword="true"/> if the object was successfully reset and can be returned to the pool;
    /// otherwise, <see langword="false"/> to indicate the object should be discarded.
    /// </returns>
    bool TryReset(T obj);
}