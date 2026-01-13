//using System.Diagnostics.CodeAnalysis;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.DependencyInjection.Extensions;
//using Wiaoj.Abstractions;
//using Wiaoj.Concurrency;
//using Wiaoj.Preconditions;

//namespace Wiaoj.Extensions.DependencyInjection;
///// <summary>
///// Provides extension methods for registering Wiaoj asynchronous factory and lazy initialization services.
///// These extensions facilitate the "Async Initialization" pattern in Dependency Injection.
///// </summary>
//public static class AsyncFactoryServiceCollectionExtensions {

//    #region 1. Basic Factory Registration

//    /// <summary>
//    /// Registers an implementation <typeparamref name="TFactory"/> for the <see cref="IAsyncFactory{TService}"/> interface.
//    /// </summary>
//    /// <typeparam name="TService">The service type being created.</typeparam>
//    /// <typeparam name="TFactory">The factory implementation type.</typeparam>
//    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
//    /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the factory. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
//    /// <returns>A reference to this instance after the operation has completed.</returns>
//    public static IServiceCollection AddAsyncFactory<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(
//        this IServiceCollection services,
//        ServiceLifetime lifetime = ServiceLifetime.Transient)
//        where TService : class
//        where TFactory : class, IAsyncFactory<TService> {

//        Preca.ThrowIfNull(services);

//        services.TryAdd(new ServiceDescriptor(typeof(IAsyncFactory<TService>), typeof(TFactory), lifetime));
//        return services;
//    }

//    #endregion

//    #region 2. AsyncLazy with Class-Based Factory

//    /// <summary>
//    /// Registers an <see cref="AsyncLazy{TService}"/> using a dedicated factory class.
//    /// Ideal for complex initialization logic encapsulated in a separate <see cref="IAsyncFactory{TService}"/> class.
//    /// </summary>
//    /// <remarks>
//    /// By default, the <see cref="AsyncLazy{TService}"/> is registered as <b>Singleton</b> to ensure the expensive initialization happens only once.
//    /// The factory is registered as <b>Transient</b>.
//    /// </remarks>
//    /// <typeparam name="TService">The service type to be lazily initialized.</typeparam>
//    /// <typeparam name="TFactory">The factory implementation that creates the service.</typeparam>
//    /// <param name="services">The service collection.</param>
//    /// <param name="lazyLifetime">The lifetime of the <see cref="AsyncLazy{TService}"/>. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
//    /// <param name="factoryLifetime">The lifetime of the factory. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
//    public static IServiceCollection AddAsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(
//        this IServiceCollection services,
//        ServiceLifetime lazyLifetime = ServiceLifetime.Singleton,
//        ServiceLifetime factoryLifetime = ServiceLifetime.Transient)
//        where TService : class
//        where TFactory : class, IAsyncFactory<TService> {

//        Preca.ThrowIfNull(services);

//        // 1. Register the factory (Implementation of IAsyncFactory<T>)
//        services.AddAsyncFactory<TService, TFactory>(factoryLifetime);

//        // 2. Register the AsyncLazy<T> wrapper
//        services.TryAdd(new ServiceDescriptor(typeof(AsyncLazy<TService>), provider => {
//            IAsyncFactory<TService> factory = provider.GetRequiredService<IAsyncFactory<TService>>();
//            return new AsyncLazy<TService>(factory);
//        }, lazyLifetime));

//        return services;
//    }

//    /// <summary>
//    /// Registers an <see cref="AsyncLazy{TService}"/> where the service type itself implements <see cref="IAsyncFactory{TService}"/>.
//    /// Useful for self-initializing services.
//    /// </summary>
//    public static IServiceCollection AddAsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(
//        this IServiceCollection services,
//        ServiceLifetime lazyLifetime = ServiceLifetime.Singleton)
//        where TService : class, IAsyncFactory<TService> {

//        return services.AddAsyncLazy<TService, TService>(lazyLifetime, ServiceLifetime.Transient);
//    }

//    #endregion

//    #region 3. AsyncLazy with Delegate Factory

//    /// <summary>
//    /// Registers an <see cref="AsyncLazy{TService}"/> using a delegate (lambda) factory.
//    /// Ideal for simple initialization logic where creating a separate Factory class is overkill.
//    /// </summary>
//    /// <typeparam name="TService">The type of service to be lazily initialized.</typeparam>
//    /// <param name="services">The service collection.</param>
//    /// <param name="factory">A delegate that receives <see cref="IServiceProvider"/> and <see cref="CancellationToken"/> to create the service.</param>
//    /// <param name="lazyLifetime">The lifetime of the <see cref="AsyncLazy{TService}"/>. Defaults to <see cref="ServiceLifetime.Singleton"/>.</param>
//    public static IServiceCollection AddAsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TService>(
//        this IServiceCollection services,
//        Func<IServiceProvider, CancellationToken, Task<TService>> factory,
//        ServiceLifetime lazyLifetime = ServiceLifetime.Singleton)
//        where TService : class {

//        Preca.ThrowIfNull(services);
//        Preca.ThrowIfNull(factory);

//        services.TryAdd(new ServiceDescriptor(typeof(AsyncLazy<TService>), provider => {
//            // Create a closure that captures the service provider
//            return new AsyncLazy<TService>(token => factory(provider, token));
//        }, lazyLifetime));

//        return services;
//    }

//    #endregion
//}