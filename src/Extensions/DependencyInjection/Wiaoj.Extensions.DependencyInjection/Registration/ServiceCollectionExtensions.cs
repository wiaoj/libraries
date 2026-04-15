using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// General extension methods for <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <summary>
    /// Replaces an existing service registration with a new implementation.
    /// If the service is not registered, it adds the new registration.
    /// </summary>
    /// <typeparam name="TService">The service type to replace.</typeparam>
    /// <typeparam name="TImplementation">The new implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The lifetime of the new registration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Replace<TService, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services, ServiceLifetime lifetime)
        where TService : class
        where TImplementation : class, TService {

        ServiceDescriptor descriptor = new(typeof(TService), typeof(TImplementation), lifetime);
        services.Replace(descriptor);
        return services;
    }

    /// <summary>
    /// Replaces an existing service registration with a specific instance.
    /// </summary>
    public static IServiceCollection Replace<TService>(this IServiceCollection services, TService instance)
        where TService : class {

        services.Replace(ServiceDescriptor.Singleton(typeof(TService), instance));
        return services;
    }

    /// <summary>
    /// Replaces an existing service registration with a factory function.
    /// Useful for complex initialization where you need to 'new' up the object manually.
    /// </summary>
    public static IServiceCollection Replace<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService> implementationFactory,
        ServiceLifetime lifetime)
        where TService : class {

        services.Replace(new ServiceDescriptor(typeof(TService), sp => implementationFactory(sp), lifetime));
        return services;
    }

    /// <summary>
    /// Adds support for <see cref="Lazy{T}"/> resolution. 
    /// This allows injecting <c>Lazy&lt;IMyService&gt;</c> into constructors to delay instantiation.
    /// Warning: Injecting <see cref="Lazy{T}"/> <see langword="where" /> <see cref="{T}" /> is a Scoped service into a Singleton service will cause a captive dependency exception when .Value is accessed.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLazySupport(this IServiceCollection services) {
        services.TryAddTransient(typeof(Lazy<>), typeof(LazyServiceProxy<>));
        return services;
    }

    /// <summary>
    /// Internal proxy to bridge IServiceProvider with System.Lazy.
    /// </summary>
    /// <typeparam name="T">The type of service to resolve lazily.</typeparam>
    private class LazyServiceProxy<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T> : Lazy<T> where T : class {
        public LazyServiceProxy(IServiceProvider provider)
            : base(() => provider.GetRequiredService<T>()) { // FIXED: Wrapped in Func<> to ensure actual lazy evaluation.
        }
    }
}