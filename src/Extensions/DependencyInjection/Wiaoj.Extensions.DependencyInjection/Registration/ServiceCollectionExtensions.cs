using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// General extension methods for <see cref="IServiceCollection"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Shadowing hazard.</b> This class is deliberately declared in the framework namespace
/// <c>Microsoft.Extensions.DependencyInjection</c> so consumers see these extensions with only
/// <c>using Microsoft.Extensions.DependencyInjection;</c>. Inside that namespace, this class's own methods are
/// closer candidates than framework extensions that are visible only through a <c>using</c> directive
/// (for example <see cref="Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions"/>).
/// A same-name call such as <c>services.Replace(descriptor)</c> therefore binds back to this class instead of the
/// framework and used to recurse until the process died with a stack overflow (issue #217).
/// </para>
/// <para>
/// <b>Rule for any method added here:</b> if its name matches an extension method that the framework (or another
/// using-imported namespace) declares for <see cref="IServiceCollection"/>, never call that framework method with
/// extension-method syntax from inside this class or namespace — call it as an explicit static invocation, e.g.
/// <c>ServiceCollectionDescriptorExtensions.Replace(services, descriptor)</c>, so the framework overload is selected
/// regardless of namespace proximity.
/// </para>
/// </remarks>
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
        // Explicit static call: inside this namespace services.Replace(descriptor) binds back to this class (issue #217).
        ServiceCollectionDescriptorExtensions.Replace(services, descriptor);
        return services;
    }

    /// <summary>
    /// Replaces an existing service registration with a specific instance.
    /// </summary>
    public static IServiceCollection Replace<TService>(this IServiceCollection services, TService instance)
        where TService : class {

        // Explicit static call: inside this namespace services.Replace(...) would bind back to this overload and recurse (issue #217).
        ServiceCollectionDescriptorExtensions.Replace(services, ServiceDescriptor.Singleton(typeof(TService), instance));
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

        // Explicit static call: inside this namespace services.Replace(...) would bind back to this class and recurse (issue #217).
        ServiceCollectionDescriptorExtensions.Replace(services, new ServiceDescriptor(typeof(TService), sp => implementationFactory(sp), lifetime));
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