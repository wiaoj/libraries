using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.ObjectPool;
using Wiaoj.ObjectPool.Internal;

namespace Wiaoj.ObjectPool;
/// <summary>
/// Provides extension methods for setting up object pooling in an <see cref="IServiceCollection"/>.
/// </summary>
public static class PoolingServiceCollectionExtensions {
    /// <summary>
    /// Ensures that the core object pooling services, such as <see cref="ObjectPoolProvider"/>, are registered.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection TryAddObjectPoolProvider(this IServiceCollection services) {
        services.TryAddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="ObjectPool{T}"/> for the specified type <typeparamref name="T"/>,
    /// configured with the provided <typeparamref name="TPolicy"/>.
    /// </summary>
    /// <remarks>
    /// This method also ensures that the core <see cref="ObjectPoolProvider"/> and the policy <typeparamref name="TPolicy"/> are registered as singletons.
    /// </remarks>
    /// <typeparam name="T">
    /// The type of object to pool. Must be a <see langword="class"/> and implement <see cref="IResettable"/>.
    /// </typeparam>
    /// <typeparam name="TPolicy">
    /// The <see cref="IPooledObjectPolicy{T}"/> that defines the pooling behavior for <typeparamref name="T"/>.
    /// </typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configureOptions">An optional action to configure the pool's behavior, such as its capacity.</param>  
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection TryAddPooledObject<T, TPolicy>(this IServiceCollection services, Action<ObjectPoolOptions> configureOptions)
        where T : class
        where TPolicy : class, IPoolPolicy<T> {

        services.TryAddObjectPoolProvider();
        services.TryAddSingleton<TPolicy>();

        services.TryAddSingleton<IObjectPool<T>>(serviceProvider => {
            TPolicy policy = serviceProvider.GetRequiredService<TPolicy>();

            ObjectPoolOptions options = new();
            configureOptions?.Invoke(options);

            MicrosoftPooledObjectPolicyAdapter<T> microsoftPolicy = new(policy);

            DefaultObjectPool<T> microsoftPool = new(microsoftPolicy, options.MaximumRetained);
            return new DefaultObjectPoolAdapter<T>(microsoftPool, options);
        });

        return services;
    }

    /// <inheritdoc cref="TryAddPooledObject{T, TPolicy}(IServiceCollection, Action{ObjectPoolOptions})"/>
    public static IServiceCollection TryAddPooledObject<T, TPolicy>(this IServiceCollection services)
        where T : class
        where TPolicy : class, IPoolPolicy<T> {
        return services.TryAddPooledObject<T, TPolicy>(_ => { });
    }

    /// <summary>
    /// Registers a singleton <see cref="ObjectPool{T}"/> using factory and resetter delegates,
    /// eliminating the need for a separate policy class for simple cases.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must be a class.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="factory">A delegate that creates a new instance of <typeparamref name="T"/>.</param>
    /// <param name="resetter">A delegate that resets an instance of <typeparamref name="T"/> to its default state.</param>
    /// <param name="configureOptions">An optional action to configure the pool's behavior, such as its capacity.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection TryAddPooledObject<T>(
        this IServiceCollection services,
        Func<T> factory,
        Predicate<T> resetter,
        Action<ObjectPoolOptions> configureOptions) where T : class {
        services.TryAddObjectPoolProvider();

        services.TryAddSingleton<IObjectPool<T>>(serviceProvider => {
            LambdaPooledObjectPolicy<T> policy = new(factory, resetter);

            ObjectPoolOptions options = new();
            configureOptions?.Invoke(options);

            MicrosoftPooledObjectPolicyAdapter<T> microsoftPolicy = new(policy);

            DefaultObjectPool<T> microsoftPool = new(microsoftPolicy, options.MaximumRetained);

            return new DefaultObjectPoolAdapter<T>(microsoftPool, options);
        });

        return services;
    }

    /// <inheritdoc cref="TryAddPooledObject{T}(IServiceCollection, Func{T}, Predicate{T}, Action{ObjectPoolOptions}?)"/> 
    public static IServiceCollection TryAddPooledObject<T>(this IServiceCollection services, Func<T> factory, Predicate<T> resetter) where T : class {
        services.TryAddPooledObject(factory, resetter, (_) => { });

        return services;
    }

    /// <summary>
    /// Registers a singleton object pool for the specified type <typeparamref name="T"/>,
    /// which must implement IResettable. This is the simplest way to pool self-resetting objects.
    /// </summary>
    /// <typeparam name="T">The type of object to pool. Must be a class, implement IResettable, and have a parameterless constructor.</typeparam>
    public static IServiceCollection TryAddPooledObject<T>(this IServiceCollection services, Action<ObjectPoolOptions>? configureOptions = null)
        where T : class, IResettable, new() {
        return services.TryAddPooledObject<T, ResettableObjectPolicy<T>>(configureOptions ?? (_ => { }));
    }

    /// <summary>
    /// Registers a singleton ObjectPool for the specified type, configured with the provided policy type.
    /// This is the non-generic version of TryAddPooledObject.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="objectType">
    /// The type of object to pool. Must be a class and implement IResettable.
    /// </param>
    /// <param name="policyType">
    /// The IPooledObjectPolicy implementation for the objectType. Must be a class.
    /// </param>
    /// <param name="configureOptions">An optional action to configure the pool's behavior, such as its capacity.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    public static IServiceCollection TryAddPooledObject(
        this IServiceCollection services,
        Type objectType,
        Type policyType,
        Action<ObjectPoolOptions> configureOptions) {

        ArgumentNullException.ThrowIfNull(objectType);
        if (!objectType.IsClass) throw new ArgumentException($"The provided objectType '{objectType.Name}' must be a class.", nameof(objectType));

        ArgumentNullException.ThrowIfNull(policyType);
        if (!policyType.IsClass) throw new ArgumentException($"The provided policyType '{policyType.Name}' must be a class.", nameof(policyType));

        if (!typeof(IResettable).IsAssignableFrom(objectType)) throw new ArgumentException($"The provided objectType '{objectType.Name}' must implement IResettable.", nameof(objectType));

        Type expectedPolicyInterface = typeof(IPoolPolicy<>).MakeGenericType(objectType);
        if (!expectedPolicyInterface.IsAssignableFrom(policyType)) {
            throw new ArgumentException(
                $"The provided policyType '{policyType.Name}' must implement '{expectedPolicyInterface.Name}'.",
                nameof(policyType));
        }


        services.TryAddObjectPoolProvider();

        // EX: TryAddSingleton(typeof(CacheContextPolicy<string, User>), typeof(CacheContextPolicy<string, User>))
        services.TryAddSingleton(policyType, policyType);

        // objectPoolType -> ObjectPool<CacheContext<string, User>>
        Type iObjectPoolType = typeof(IObjectPool<>).MakeGenericType(objectType);
        Type microsoftObjectPoolType = typeof(ObjectPool<>).MakeGenericType(objectType);

        services.TryAddSingleton(iObjectPoolType, serviceProvider => {
            ObjectPoolProvider provider = serviceProvider.GetRequiredService<ObjectPoolProvider>();
            object policy = serviceProvider.GetRequiredService(policyType);

            MethodInfo createMethod = typeof(ObjectPoolProvider)
                .GetMethod(nameof(ObjectPoolProvider.Create), [typeof(IPooledObjectPolicy<>)])!
                .MakeGenericMethod(objectType);

            Debug.Assert(createMethod != null, "ObjectPoolProvider.Create<T>() method could not be found via reflection.");

            ObjectPoolOptions options = new();
            configureOptions?.Invoke(options);

            Type adapterType = typeof(MicrosoftPooledObjectPolicyAdapter<>).MakeGenericType(objectType);
            object microsoftPolicy = Activator.CreateInstance(adapterType, policy)!;

            Type microsoftPoolType = typeof(DefaultObjectPool<>).MakeGenericType(objectType);
            object microsoftPool = Activator.CreateInstance(microsoftPoolType, microsoftPolicy, options.MaximumRetained)!;

            Type poolAdapterType = typeof(DefaultObjectPoolAdapter<>).MakeGenericType(objectType);
            return Activator.CreateInstance(poolAdapterType, microsoftPool, options)!;
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton asynchronous IAsyncObjectPool for the specified type,
    /// configured with the provided async policy.
    /// </summary>
    public static IServiceCollection TryAddAsyncPooledObject<T, TPolicy>(this IServiceCollection services, Action<ObjectPoolOptions>? configureOptions = null)
        where T : class
        where TPolicy : class, IAsyncPoolPolicy<T> {
        services.TryAddSingleton<TPolicy>();
        services.TryAddSingleton<IAsyncObjectPool<T>>(provider => {
            var policy = provider.GetRequiredService<TPolicy>();
            var options = new ObjectPoolOptions();
            configureOptions?.Invoke(options);
            return ObjectPoolFactory.CreateAsync(policy, options);
        });
        return services;
    }

    /// <summary>
    /// Registers a singleton asynchronous IAsyncObjectPool using factory and resetter delegates.
    /// </summary>
    public static IServiceCollection TryAddAsyncPooledObject<T>(
        this IServiceCollection services,
        Func<CancellationToken, ValueTask<T>> factory,
        Func<T, ValueTask<bool>> resetter,
        Action<ObjectPoolOptions>? configureOptions = null) where T : class {
        services.TryAddSingleton<IAsyncObjectPool<T>>(provider => {
            var policy = new LambdaAsyncPooledObjectPolicy<T>(factory, resetter);
            var options = new ObjectPoolOptions();
            configureOptions?.Invoke(options);
            return ObjectPoolFactory.CreateAsync(policy, options);
        });
        return services;
    }
}