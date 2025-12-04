using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Abstractions;
using Wiaoj.Concurrency;

namespace Wiaoj.Extensions.DependencyInjection;
/// <summary>
/// Provides extension methods for registering Wiaoj asynchronous factory and lazy initialization services.
/// </summary>
public static class AsyncFactoryServiceCollectionExtensions {
    /// <summary>
    /// Registers an implementation <typeparamref name="TFactory"/> for the <see cref="IAsyncFactory{TService}"/> interface.
    /// </summary>
    /// <typeparam name="TService">The service type being created.</typeparam>
    /// <typeparam name="TFactory">The factory implementation type.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <param name="lifetime">The <see cref="ServiceLifetime"/> of the factory. Defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IServiceCollection AddAsyncFactory<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(this IServiceCollection services, ServiceLifetime lifetime = ServiceLifetime.Transient)
        where TService : class
        where TFactory : class, IAsyncFactory<TService> {
        services.TryAdd(new ServiceDescriptor(typeof(IAsyncFactory<TService>), typeof(TFactory), lifetime));
        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="AsyncLazy{TService}"/> where the service itself
    /// implements <see cref="IAsyncFactory{TService}"/>.
    /// </summary>
    /// <remarks>
    /// This is a convenient shortcut for services that contain their own asynchronous initialization logic.
    /// The service/factory (<typeparamref name="TService"/>) is registered as transient.
    /// </remarks>
    public static IServiceCollection AddAsyncLazy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>(this IServiceCollection services)
        where TService : class, IAsyncFactory<TService> {
        services.AddAsyncFactory<TService, TService>(ServiceLifetime.Transient);

        services.TryAddSingleton(provider => {
            IAsyncFactory<TService> factory = provider.GetRequiredService<IAsyncFactory<TService>>();
            return new AsyncLazy<TService>(factory);
        });

        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="AsyncLazy{TService}"/> and its corresponding <see cref="IAsyncFactory{TService}"/> implementation.
    /// This is the primary helper method for registering lazily and asynchronously initialized services.
    /// </summary>
    /// <remarks>
    /// The <see cref="AsyncLazy{TService}"/> instance is always registered as a singleton to ensure the underlying value is created only once.
    /// The factory, <typeparamref name="TFactory"/>, is registered as transient by default.
    /// </remarks>
    /// <typeparam name="TService">The service type to be lazily initialized.</typeparam>
    /// <typeparam name="TFactory">The factory implementation that creates the service.</typeparam>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IServiceCollection AddAsyncLazy<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TFactory>(this IServiceCollection services)
        where TService : class
        where TFactory : class, IAsyncFactory<TService> {
        // 1. Register the factory if it's not already there.
        // Factories are typically stateless and should be transient.
        services.AddAsyncFactory<TService, TFactory>(ServiceLifetime.Transient);

        // 2. Register the AsyncLazy<T> instance as a singleton.
        // This is crucial to ensure the value is created only once for the entire application lifetime.
        services.TryAddSingleton(provider => {
            IAsyncFactory<TService> factory = provider.GetRequiredService<IAsyncFactory<TService>>();
            return new AsyncLazy<TService>(factory);
        });

        return services;
    } 
}