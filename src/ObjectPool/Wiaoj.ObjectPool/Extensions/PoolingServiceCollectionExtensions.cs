using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using System.Diagnostics.CodeAnalysis;
using Wiaoj.Abstractions;
using Wiaoj.ObjectPool.Policies;

namespace Wiaoj.ObjectPool.Extensions;
/// <summary>
/// Provides streamlined extension methods for registering Object Pools in DI.
/// </summary>
public static class PoolingServiceCollectionExtensions {

    /// <summary>
    /// Ensures the core provider is registered.
    /// </summary>
    public static IServiceCollection TryAddObjectPoolProvider(this IServiceCollection services) {
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        return services;
    }

    /// <summary>
    /// Ensures the core provider is registered.
    /// </summary>
    public static IServiceCollection AddObjectPoolProvider(this IServiceCollection services) {
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        return services;
    }

    #region Synchronous Pools

    /// <summary>
    /// Registers a standard synchronous pool for type <typeparamref name="T"/>.
    /// <typeparamref name="T"/> must have a parameterless constructor.
    /// </summary>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where T : class, new() {
        return services.RegisterPool(new DefaultPoolPolicy<T>(), configure);
    }

    /// <summary>
    /// Registers a pool for <see cref="IResettable"/> types. 
    /// Reset logic is handled automatically by the object.
    /// </summary>
    public static IServiceCollection AddResettablePool<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where T : class, IResettable, new() {
        return services.RegisterPool(new ResettableObjectPolicy<T>(), configure);
    }

    /// <summary>
    /// Registers a pool with custom factory and reset delegates.
    /// </summary>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, Func<T> factory, Predicate<T> resetter, Action<ObjectPoolOptions>? configure = null)
        where T : class {
        return services.RegisterPool(new LambdaPooledObjectPolicy<T>(factory, resetter), configure);
    }

    /// <summary>
    /// Registers a pool with a completely custom <see cref="IPoolPolicy{T}"/>.
    /// </summary>
    public static IServiceCollection AddObjectPool<T>(this IServiceCollection services, IPoolPolicy<T> policy, Action<ObjectPoolOptions>? configure = null)
        where T : class {
        return services.RegisterPool(policy, configure);
    }

    #endregion

    #region Asynchronous Pools

    /// <summary>
    /// Registers an async pool for <see cref="IResettable"/> types (Sync reset).
    /// </summary>
    public static IServiceCollection AddAsyncPool<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where T : class, IResettable, new() {
        return services.RegisterAsyncPool(new ResettableObjectPolicy<T>(), configure);
    }

    /// <summary>
    /// Registers an async pool for <see cref="IAsyncResettable"/> types (True async reset).
    /// </summary>
    public static IServiceCollection AddAsyncResettablePool<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where T : class, IAsyncResettable, new() {
        // Factory null geçiliyor, policy içinde T new() ile oluşturulacak.
        var policy = new AsyncResettableObjectPolicy<T>(factory: null);
        return services.RegisterAsyncPool(policy, configure);
    }

    /// <summary>
    /// Registers an async pool using custom async delegates.
    /// </summary>
    public static IServiceCollection AddAsyncPool<T>(this IServiceCollection services, Func<CancellationToken, ValueTask<T>> factory, Func<T, ValueTask<bool>> resetter, Action<ObjectPoolOptions>? configure = null)
        where T : class {
        return services.RegisterAsyncPool(new LambdaAsyncPooledObjectPolicy<T>(factory, resetter), configure);
    }

    /// <summary>
    /// Registers an async pool with a custom <see cref="IAsyncPoolPolicy{T}"/>.
    /// </summary>
    public static IServiceCollection AddAsyncPool<T>(this IServiceCollection services, IAsyncPoolPolicy<T> policy, Action<ObjectPoolOptions>? configure = null)
        where T : class {
        return services.RegisterAsyncPool(policy, configure);
    }

    #endregion

    #region Factory Integration (IAsyncFactory)

    /// <summary>
    /// Registers an async pool that resolves an <see cref="IAsyncFactory{T}"/> from DI.
    /// Automatically handles reset if T implements <see cref="IResettable"/> or <see cref="IAsyncResettable"/>.
    /// </summary>
    public static IServiceCollection AddAsyncFactoryPool<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where T : class {

        // Bu metod akıllı davranır: T'nin tipine göre reset mantığını seçer.
        Func<T, ValueTask<bool>> resetLogic;

        if(typeof(IAsyncResettable).IsAssignableFrom(typeof(T))) {
            resetLogic = obj => ((IAsyncResettable)obj).TryResetAsync();
        }
        else if(typeof(IResettable).IsAssignableFrom(typeof(T))) {
            resetLogic = obj => new ValueTask<bool>(((IResettable)obj).TryReset());
        }
        else {
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' must implement IResettable or IAsyncResettable to use auto-factory pooling. " +
                $"Otherwise, specify a reset delegate.");
        }

        return services.AddAsyncFactoryPoolInternal(resetLogic, configure);
    }

    /// <summary>
    /// Registers an async pool that resolves an <see cref="IAsyncFactory{T}"/> from DI, 
    /// using a custom reset delegate.
    /// </summary>
    public static IServiceCollection AddAsyncFactoryPool<T>(this IServiceCollection services, Func<T, ValueTask<bool>> resetter, Action<ObjectPoolOptions>? configure = null)
        where T : class {
        return services.AddAsyncFactoryPoolInternal(resetter, configure);
    }

    #endregion

    #region Generic Policy Registration (DI Support)

    /// <summary>
    /// Registers a synchronous object pool where the Policy type is also resolved via DI.
    /// This allows injecting dependencies (like ILogger, IConfiguration) into your Policy constructor.
    /// </summary>
    /// <typeparam name="TObject">The type of object to pool.</typeparam>
    /// <typeparam name="TPolicy">The type of the policy, which must implement IPoolPolicy<TObject>.</typeparam>
    public static IServiceCollection AddObjectPool<TObject, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where TObject : class
        where TPolicy : class, IPoolPolicy<TObject> {

        services.TryAddObjectPoolProvider();

        services.TryAddSingleton<IObjectPool<TObject>>(sp => {
            // ActivatorUtilities, TPolicy'yi oluştururken constructor'ındaki 
            // parametreleri DI container'dan otomatik çeker.
            var policy = ActivatorUtilities.CreateInstance<TPolicy>(sp);

            var options = new ObjectPoolOptions();
            configure?.Invoke(options);

            return ObjectPoolFactory.Create(policy, options);
        });

        return services;
    }

    /// <summary>
    /// Registers an ASYNCHRONOUS object pool where the Async Policy type is resolved via DI.
    /// This allows injecting dependencies into your Async Policy constructor.
    /// </summary>
    /// <typeparam name="TObject">The type of object to pool.</typeparam>
    /// <typeparam name="TPolicy">The type of the policy, which must implement IAsyncPoolPolicy<TObject>.</typeparam>
    public static IServiceCollection AddAsyncPool<TObject, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPolicy>(this IServiceCollection services, Action<ObjectPoolOptions>? configure = null)
        where TObject : class
        where TPolicy : class, IAsyncPoolPolicy<TObject> {

        services.TryAddObjectPoolProvider();

        services.TryAddSingleton<IAsyncObjectPool<TObject>>(sp => {
            // Policy oluşturulurken DI servisleri enjekte edilir.
            var policy = ActivatorUtilities.CreateInstance<TPolicy>(sp);

            var options = new ObjectPoolOptions();
            configure?.Invoke(options);

            return ObjectPoolFactory.CreateAsync(policy, options);
        });

        return services;
    }

    #endregion

    #region Internal Helpers

    private static IServiceCollection RegisterPool<T>(this IServiceCollection services, IPoolPolicy<T> policy, Action<ObjectPoolOptions>? configure)
        where T : class {
        services.TryAddObjectPoolProvider();
        services.TryAddSingleton<IObjectPool<T>>(sp => {
            var options = new ObjectPoolOptions();
            configure?.Invoke(options);
            return ObjectPoolFactory.Create(policy, options);
        });
        return services;
    }

    private static IServiceCollection RegisterAsyncPool<T>(this IServiceCollection services, IAsyncPoolPolicy<T> policy, Action<ObjectPoolOptions>? configure)
        where T : class {
        services.TryAddObjectPoolProvider();
        services.TryAddSingleton<IAsyncObjectPool<T>>(sp => {
            var options = new ObjectPoolOptions();
            configure?.Invoke(options);
            return ObjectPoolFactory.CreateAsync(policy, options);
        });
        return services;
    }

    private static IServiceCollection AddAsyncFactoryPoolInternal<T>(this IServiceCollection services, Func<T, ValueTask<bool>> resetter, Action<ObjectPoolOptions>? configure)
        where T : class {
        services.TryAddObjectPoolProvider();
        services.TryAddSingleton<IAsyncObjectPool<T>>(sp => {
            var factory = sp.GetService<IAsyncFactory<T>>()
                          ?? throw new InvalidOperationException($"No 'IAsyncFactory<{typeof(T).Name}>' found in DI.");

            var policy = new AsyncFactoryPooledObjectPolicy<T>(factory, resetter);

            var options = new ObjectPoolOptions();
            configure?.Invoke(options);

            return ObjectPoolFactory.CreateAsync(policy, options);
        });
        return services;
    }

    #endregion
}