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

/// <summary>
/// Asynchronously creates an instance of type <typeparamref name="T"/> with one argument.
/// </summary>
/// <typeparam name="T">The type to create.</typeparam>
/// <typeparam name="T1">The type of the first argument.</typeparam>
public interface IAsyncFactory<T, in T1> {
    /// <summary>
    /// Asynchronously creates an instance of <typeparamref name="T"/> using the specified argument.
    /// </summary>
    /// <param name="arg1">The first argument for instance creation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous creation of the instance.</returns>
    Task<T> CreateAsync(T1 arg1, CancellationToken cancellationToken = default);
}

/// <summary>
/// Asynchronously creates an instance of type <typeparamref name="T"/> with two arguments.
/// </summary>
/// <typeparam name="T">The type to create.</typeparam>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
public interface IAsyncFactory<T, in T1, in T2> {
    /// <summary>
    /// Asynchronously creates an instance of <typeparamref name="T"/> using the specified arguments.
    /// </summary>
    /// <param name="arg1">The first argument for instance creation.</param>
    /// <param name="arg2">The second argument for instance creation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous creation of the instance.</returns>
    Task<T> CreateAsync(T1 arg1, T2 arg2, CancellationToken cancellationToken = default);
}

/// <summary>
/// Asynchronously creates an instance of type <typeparamref name="T"/> with three arguments.
/// </summary>
/// <typeparam name="T">The type to create.</typeparam>
/// <typeparam name="T1">The type of the first argument.</typeparam>
/// <typeparam name="T2">The type of the second argument.</typeparam>
/// <typeparam name="T3">The type of the third argument.</typeparam>
public interface IAsyncFactory<T, in T1, in T2, in T3> {
    /// <summary>
    /// Asynchronously creates an instance of <typeparamref name="T"/> using the specified arguments.
    /// </summary>
    /// <param name="arg1">The first argument for instance creation.</param>
    /// <param name="arg2">The second argument for instance creation.</param>
    /// <param name="arg3">The third argument for instance creation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous creation of the instance.</returns>
    Task<T> CreateAsync(T1 arg1, T2 arg2, T3 arg3, CancellationToken cancellationToken = default);
}