namespace Wiaoj.ObjectPool;
/// <summary>
/// Defines a contract for an object pool, abstracting the underlying implementation.
/// This is the preferred type to inject into services for leasing pooled objects.
/// </summary>
/// <typeparam name="T">The type of object being pooled.</typeparam>
public interface IObjectPool<T> where T : class {
    /// <summary>
    /// Leases an object from the pool within a disposable scope using a 'using' block.
    /// When the returned <see cref="PooledObject{T}"/> is disposed, the leased object is automatically returned to the pool.
    /// This is the safest and recommended way to manage the lifetime of a pooled object.
    /// </summary>
    /// <remarks>
    /// For manual lifecycle management, see the <see cref="Get"/> method.
    /// </remarks>
    /// <returns>
    /// A <see cref="PooledObject{T}"/> instance that manages the lifetime of the leased object.
    /// </returns>
    PooledObject<T> Lease();

    /// <summary>
    /// Gets an object from the pool whose lifetime must be managed manually.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: An object obtained via this method MUST be returned to the pool by calling <see cref="Return"/>
    /// when it's no longer needed. Failure to do so will result in an object leak.
    /// For automatic return functionality, prefer using the <see cref="Lease"/> method.
    /// </remarks>
    /// <returns>An object from the pool that must be manually returned.</returns>
    T Get();

    /// <summary>
    /// Returns an object to the pool that was previously obtained via the <see cref="Get"/> method.
    /// </summary>
    /// <param name="object">The object to return to the pool.</param>
    void Return(T @object);
}