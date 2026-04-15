using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Provides extension methods for applying the Decorator pattern to services in an <see cref="IServiceCollection"/>.
/// These extensions compile the decoration chain at registration time to ensure maximum runtime performance.
/// </summary>
public static class DecoratorServiceCollectionExtensions {

    #region Basic Decoration (Closed & Open)

    /// <summary>
    /// Decorates all registered services of type <typeparamref name="TService"/> with the specified <typeparamref name="TDecorator"/>.
    /// Throws an exception if the service is not registered.
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <typeparam name="TDecorator">The decorator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Decorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService {
        return services.Decorate(typeof(TService), typeof(TDecorator));
    }

    /// <summary>
    /// Tries to decorate all registered services of type <typeparamref name="TService"/> with the specified <typeparamref name="TDecorator"/>.
    /// Does nothing if the service is not registered.
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <typeparam name="TDecorator">The decorator type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection TryDecorate<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService {
        return services.DecorateInternal(typeof(TService), typeof(TDecorator), throwOnNotFound: false);
    }

    /// <summary>
    /// Decorates all registered services of the specified <paramref name="serviceType"/> with <paramref name="decoratorType"/>.
    /// Throws an exception if the service is not registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceType">The service type to decorate.</param>
    /// <param name="decoratorType">The decorator type.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType) {
        return services.DecorateInternal(serviceType, decoratorType, throwOnNotFound: true);
    }

    #endregion

    #region Predicate Based Decoration

    /// <summary>
    /// Decorates registered services of the specified <paramref name="serviceType"/> that match the given <paramref name="predicate"/>.
    /// Throws an exception if no matching services are found.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceType">The service type to decorate.</param>
    /// <param name="decoratorType">The decorator type.</param>
    /// <param name="predicate">A function to test each descriptor.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Decorate(this IServiceCollection services, Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType, Func<ServiceDescriptor, bool> predicate) {
        return services.DecorateInternal(serviceType, decoratorType, throwOnNotFound: true, predicate);
    }

    #endregion

    #region Factory Based Decoration

    /// <summary>
    /// Decorates all registered services of type <typeparamref name="TService"/> using a custom factory function.
    /// </summary>
    /// <typeparam name="TService">The service type to decorate.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="decoratorFactory">The factory that creates the decorator instance, receiving the inner instance and the service provider.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Decorate<TService>(this IServiceCollection services, Func<TService, IServiceProvider, TService> decoratorFactory)
        where TService : class {

        // Take a snapshot to avoid modified collection exceptions during iteration
        List<ServiceDescriptor> descriptors = [.. services.Where(d => d.ServiceType == typeof(TService))];

        if(descriptors.Count == 0)
            throw new InvalidOperationException($"Service '{typeof(TService).Name}' not found.");

        foreach(ServiceDescriptor descriptor in descriptors) {
            int index = services.IndexOf(descriptor);

            // 1. Compile the base descriptor into a flat factory
            Func<IServiceProvider, object?, object> baseFactory = CompileBaseFactory(descriptor);

            // 2. Wrap it with the user-provided decorator factory
            services[index] = DecorateDescriptor(descriptor, (provider, key) => {
                TService innerInstance = (TService)baseFactory(provider, key);
                return decoratorFactory(innerInstance, provider);
            });
        }

        return services;
    }

    #endregion

    #region Internal Core Logic

    private static IServiceCollection DecorateInternal(
    this IServiceCollection services,
    Type serviceType,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type decoratorType,
    bool throwOnNotFound,
    Func<ServiceDescriptor, bool>? predicate = null) {

        List<ServiceDescriptor> descriptors = [.. services.Where(d => d.ServiceType == serviceType && (predicate?.Invoke(d) ?? true))];

        if(descriptors.Count == 0) {
            if(throwOnNotFound) throw new InvalidOperationException($"No registered services found for '{serviceType.Name}'.");
            return services;
        }

        if(serviceType.IsGenericTypeDefinition) {
            // Mimarinin kararı: Hacksiz, temiz ve performanslı bir DI için Open Generic dekorasyonu native olarak engellendi.
            throw new NotSupportedException(
                $"Decorating Open Generic types ('{serviceType.Name}') is not supported natively by this library due to MS DI limitations and chaining constraints. " +
                $"Please register and decorate closed generic types explicitly (e.g., IRepository<int>).");
        }

        foreach(var descriptor in descriptors) {
            var index = services.IndexOf(descriptor);

            var baseFactory = CompileBaseFactory(descriptor);
            var decoratorObjectFactory = ActivatorUtilities.CreateFactory(decoratorType, [serviceType]);

            services[index] = DecorateDescriptor(descriptor, (provider, key) => {
                var innerInstance = baseFactory(provider, key);
                return decoratorObjectFactory(provider, [innerInstance]);
            });
        }

        return services;
    }

    /// <summary>
    /// Wraps an existing descriptor with a new factory to maintain lifetimes and keyed states.
    /// </summary>
    private static ServiceDescriptor DecorateDescriptor(ServiceDescriptor descriptor, Func<IServiceProvider, object?, object> factory) {
        if(descriptor.IsKeyedService) {
            return new ServiceDescriptor(
                descriptor.ServiceType,
                descriptor.ServiceKey,
                (sp, key) => factory(sp, key),
                descriptor.Lifetime);
        }

        return new ServiceDescriptor(
            descriptor.ServiceType,
            sp => factory(sp, null),
            descriptor.Lifetime);
    }

    /// <summary>
    /// Compiles the logic of an existing <see cref="ServiceDescriptor"/> into a flat delegate function.
    /// This resolves the original dependency graph without relying on the DI container's recursive lookups,
    /// preventing infinite loops and improving performance.
    /// </summary>
    private static Func<IServiceProvider, object?, object> CompileBaseFactory(ServiceDescriptor descriptor) {
        // 1. If it's a singleton instance
        if(descriptor.ImplementationInstance != null) {
            return (sp, key) => descriptor.ImplementationInstance;
        }

        // 2. If it's a keyed factory
        if(descriptor.IsKeyedService && descriptor.KeyedImplementationFactory != null) {
            return (sp, key) => descriptor.KeyedImplementationFactory(sp, key);
        }

        // 3. If it's a normal factory
        if(descriptor.ImplementationFactory != null) {
            return (sp, key) => descriptor.ImplementationFactory(sp);
        }

        // 4. Keyed Type
        if(descriptor.IsKeyedService && descriptor.KeyedImplementationType != null) {
            Type type = descriptor.KeyedImplementationType;
            ObjectFactory objectFactory = ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
            return (sp, key) => objectFactory(sp, null);
        }

        // 5. If it's an ImplementationType, we compile its instantiation to avoid reflection
        if(descriptor.ImplementationType != null) {
            Type type = descriptor.ImplementationType;
            // Compile the type instantiation
            ObjectFactory objectFactory = ActivatorUtilities.CreateFactory(type, Type.EmptyTypes);
            return (sp, key) => objectFactory(sp, null);
        }

        throw new InvalidOperationException($"Could not compile base factory for '{descriptor.ServiceType.Name}'. The descriptor is missing ImplementationType, Instance, and Factory.");
    }

    #endregion 
}