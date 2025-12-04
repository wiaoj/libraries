namespace Wiaoj.Abstractions;

/// <summary>
/// Defines a standardized, asynchronous factory for creating instances of a type T.
/// </summary>
/// <remarks>
/// This interface is essential for scenarios where the initialization of an object
/// involves asynchronous operations. It promotes loose coupling and is a key component
/// for Dependency Injection scenarios involving async-initialized objects.
/// </remarks>
/// <typeparam name="T">The type of object to create.</typeparam>
public interface IAsyncFactory<T> {
    /// <summary>
    /// Asynchronously creates an instance of type T.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous creation operation, resulting in an instance of T.</returns>
    Task<T> CreateAsync(CancellationToken cancellationToken = default);
}