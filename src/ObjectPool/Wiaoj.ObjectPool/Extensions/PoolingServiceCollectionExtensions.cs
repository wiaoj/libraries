using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using Wiaoj.Abstractions;
using Wiaoj.ObjectPool.Abstractions;
using Wiaoj.ObjectPool.Configuration;
using Wiaoj.ObjectPool.Core;
using Wiaoj.ObjectPool.Policies;
using IResettable = Wiaoj.ObjectPool.Abstractions.IResettable;

namespace Wiaoj.ObjectPool.Extensions;
/// <summary>
/// Provides extension methods for setting up object pooling in an <see cref="IServiceCollection"/>.
/// Includes support for Synchronous, Asynchronous, and Factory-based creation patterns.
/// </summary>
public static class PoolingServiceCollectionExtensions {
    /// <summary>
    /// Registers the core object pool provider services. 
    /// This is automatically called by other registration methods, but can be called manually if needed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPoolProvider(this IServiceCollection services) {
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        return services;
    }

    #region Standard (new T())

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using the default parameterless constructor.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must have a public parameterless constructor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services)
        where T : class, new() {
        return services.AddObjectPool<T>(_ => { });
    }

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using the default parameterless constructor, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must have a public parameterless constructor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure pool options (e.g., maximum retained capacity).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, new() {
        return services.AddObjectPool(new DefaultPoolPolicy<T>(), configureOptions);
    }

    #endregion

    #region IResettable (Self-Resetting)

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> for a type that implements <see cref="IResettable"/>.
    /// The reset logic is automatically handled by the object itself.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IResettable"/> and have a parameterless constructor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResettableObjectPool<T>(this IServiceCollection services)
        where T : class, IResettable, new() {
        return services.AddResettableObjectPool<T>(_ => { });
    }

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> for a type that implements <see cref="IResettable"/>, with custom options.
    /// The reset logic is automatically handled by the object itself.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IResettable"/> and have a parameterless constructor.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResettableObjectPool<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, IResettable, new() {
        return services.AddObjectPool(new ResettableObjectPolicy<T>(), configureOptions);
    }

    #endregion

    #region Lambda (Custom Factory/Reset)

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using custom delegates for creation and resetting.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">A delegate to create a new instance of <typeparamref name="T"/>.</param>
    /// <param name="resetter">A delegate to reset the object state. Returns <c>true</c> if successful.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, Func<T> factory, Predicate<T> resetter)
        where T : class {
        return services.AddObjectPool(factory, resetter, _ => { });
    }

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using custom delegates for creation and resetting, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">A delegate to create a new instance of <typeparamref name="T"/>.</param>
    /// <param name="resetter">A delegate to reset the object state. Returns <c>true</c> if successful.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, Func<T> factory, Predicate<T> resetter, Action<ObjectPoolOptions> configureOptions)
        where T : class {
        return services.AddObjectPool(new LambdaPooledObjectPolicy<T>(factory, resetter), configureOptions);
    }

    #endregion

    #region Policy (IPoolPolicy)

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using a custom policy implementation.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The policy instance defining creation and reset logic.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, IPoolPolicy<T> policy)
        where T : class {
        return services.AddObjectPool(policy, _ => { });
    }

    /// <summary>
    /// Registers a synchronous <see cref="IObjectPool{T}"/> using a custom policy implementation, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The policy instance defining creation and reset logic.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, IPoolPolicy<T> policy, Action<ObjectPoolOptions> configureOptions)
        where T : class {
        services.AddObjectPoolProvider();

        services.TryAddSingleton<IObjectPool<T>>(sp => {
            ObjectPoolOptions options = new();
            configureOptions(options);
            return ObjectPoolFactory.Create(policy, options);
        });
        return services;
    }

    #endregion
    #region IResettable (Sync Reset in Async Pool)
    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> for a type that implements <see cref="IResettable"/> (Synchronous Reset).
    /// Uses the object's synchronous reset logic wrapped in a ValueTask.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services)
        where T : class, IResettable, new() {
        return services.AddAsyncObjectPool<T>(_ => { });
    }

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> for a type that implements <see cref="IResettable"/> (Synchronous Reset), with custom options.
    /// Uses the object's synchronous reset logic wrapped in a ValueTask.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, IResettable, new() {
        return services.AddAsyncObjectPool(new ResettableObjectPolicy<T>(), configureOptions);
    }

    #endregion

    #region IAsyncResettable (True Async Reset)

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> for a type that implements <see cref="IAsyncResettable"/>.
    /// This allows for non-blocking cleanup (e.g., I/O) during object return.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IAsyncResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncResettableObjectPool<T>(this IServiceCollection services)
        where T : class, IAsyncResettable, new() {
        return services.AddAsyncResettableObjectPool<T>(_ => { });
    }

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> for a type that implements <see cref="IAsyncResettable"/>, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IAsyncResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncResettableObjectPool<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, IAsyncResettable, new() {
        LambdaAsyncPooledObjectPolicy<T> policy = new(
            factory: _ => new ValueTask<T>(new T()),
            resetter: obj => obj.TryResetAsync()
        );
        return services.AddAsyncObjectPool(policy, configureOptions);
    }

    #endregion

    #region Lambda (Async Factory/Reset)

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> using custom async delegates.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">An async delegate to create a new instance.</param>
    /// <param name="resetter">An async delegate to reset the object state.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services, Func<CancellationToken, ValueTask<T>> factory, Func<T, ValueTask<bool>> resetter)
        where T : class {
        return services.AddAsyncObjectPool(factory, resetter, _ => { });
    }

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> using custom async delegates, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">An async delegate to create a new instance.</param>
    /// <param name="resetter">An async delegate to reset the object state.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services, Func<CancellationToken, ValueTask<T>> factory, Func<T, ValueTask<bool>> resetter, Action<ObjectPoolOptions> configureOptions)
        where T : class {
        return services.AddAsyncObjectPool(new LambdaAsyncPooledObjectPolicy<T>(factory, resetter), configureOptions);
    }

    #endregion

    #region Policy (IAsyncPoolPolicy)

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> using a custom async policy.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The async policy instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services, IAsyncPoolPolicy<T> policy)
        where T : class {
        return services.AddAsyncObjectPool(policy, _ => { });
    }

    /// <summary>
    /// Registers an asynchronous <see cref="IAsyncObjectPool{T}"/> using a custom async policy, with custom options.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="policy">The async policy instance.</param>
    /// <param name="configureOptions">An action to configure pool options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPool<T>(this IServiceCollection services, IAsyncPoolPolicy<T> policy, Action<ObjectPoolOptions> configureOptions)
        where T : class {

        services.TryAddSingleton<IAsyncObjectPool<T>>(sp => {
            ObjectPoolOptions options = new();
            configureOptions(options);
            return ObjectPoolFactory.CreateAsync(policy, options);
        });
        return services;
    }

    #endregion

    #region Factory + Lambda Reset

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and a lambda for resetting.
    /// </summary>
    /// <typeparam name="T">The type of object to pool.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="resetter">An async delegate to reset the object state.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no <see cref="IAsyncFactory{T}"/> is found in DI.</exception>
    public static IServiceCollection AddAsyncObjectPoolFromFactory<T>(this IServiceCollection services, Func<T, ValueTask<bool>> resetter)
        where T : class {
        return services.AddAsyncObjectPoolFromFactory<T>(resetter, _ => { });
    }

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and a lambda for resetting, with custom options.
    /// </summary>
    public static IServiceCollection AddAsyncObjectPoolFromFactory<T>(this IServiceCollection services, Func<T, ValueTask<bool>> resetter, Action<ObjectPoolOptions> configureOptions)
        where T : class {
        return services.AddAsyncObjectPoolFromFactoryInternal<T>((sp, obj) => resetter(obj), configureOptions);
    }

    #endregion

    #region Factory + IResettable

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and relies on <see cref="IResettable"/> for cleanup.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncObjectPoolFromFactory<T>(this IServiceCollection services)
        where T : class, IResettable {
        return services.AddAsyncObjectPoolFromFactory<T>(_ => { });
    }

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and relies on <see cref="IResettable"/> for cleanup, with custom options.
    /// </summary>
    public static IServiceCollection AddAsyncObjectPoolFromFactory<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, IResettable {
        return services.AddAsyncObjectPoolFromFactoryInternal<T>((sp, obj) => new ValueTask<bool>(obj.TryReset()), configureOptions);
    }

    #endregion

    #region Factory + IAsyncResettable

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and relies on <see cref="IAsyncResettable"/> for cleanup.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must implement <see cref="IAsyncResettable"/>.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAsyncResettableObjectPoolFromFactory<T>(this IServiceCollection services)
        where T : class, IAsyncResettable {
        return services.AddAsyncResettableObjectPoolFromFactory<T>(_ => { });
    }

    /// <summary>
    /// Registers an <see cref="IAsyncObjectPool{T}"/> that uses a registered <see cref="IAsyncFactory{T}"/> for creation
    /// and relies on <see cref="IAsyncResettable"/> for cleanup, with custom options.
    /// </summary>
    public static IServiceCollection AddAsyncResettableObjectPoolFromFactory<T>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class, IAsyncResettable {
        return services.AddAsyncObjectPoolFromFactoryInternal<T>((sp, obj) => obj.TryResetAsync(), configureOptions);
    }

    #endregion

    private static IServiceCollection AddAsyncObjectPoolFromFactoryInternal<T>(
        this IServiceCollection services,
        Func<IServiceProvider, T, ValueTask<bool>> resetLogic,
        Action<ObjectPoolOptions> configureOptions)
        where T : class {

        services.AddObjectPoolProvider();

        services.TryAddSingleton<IAsyncObjectPool<T>>(sp => {
            // 1. DI Container'dan IAsyncFactory<T>'yi bul
            var factory = sp.GetService<IAsyncFactory<T>>();
            if (factory is null) {
                throw new InvalidOperationException(
                    $"Cannot register AsyncObjectPool for '{typeof(T).Name}' because no 'IAsyncFactory<{typeof(T).Name}>' " +
                    $"was found in the Dependency Injection container.");
            }

            // 2. Factory Policy oluştur
            AsyncFactoryPooledObjectPolicy<T> policy = new(
                factory,
                obj => resetLogic(sp, obj)
            );

            // 3. Pool oluştur
            ObjectPoolOptions options = new();
            configureOptions(options);
            return ObjectPoolFactory.CreateAsync(policy, options);
        });

        return services;
    }
}