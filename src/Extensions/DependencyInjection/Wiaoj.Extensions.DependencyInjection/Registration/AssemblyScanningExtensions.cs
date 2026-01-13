using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Wiaoj.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for assembly scanning capabilities.
/// </summary>
public static class AssemblyScanningExtensions {
    /// <summary>
    /// Scans assemblies and registers types based on the conventions defined in the configuration action.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="action">A delegate to configure the assembly scanning options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Scan(this IServiceCollection services, Action<IAssemblyScanBuilder> action) {
        AssemblyScanBuilder builder = new(services);

        // Configure the builder
        action(builder);

        // Execute the registration logic after configuration is complete
        builder.Register();

        return services;
    }
}

/// <summary>
/// Defines a builder for configuring assembly scanning and service registration.
/// </summary>
public interface IAssemblyScanBuilder {
    /// <summary>
    /// Adds the assembly containing the specified type <typeparamref name="T"/> to the scanning list.
    /// </summary>
    /// <typeparam name="T">The type used to locate the assembly.</typeparam>
    IAssemblyScanBuilder FromAssemblyOf<T>();

    /// <summary>
    /// Adds the specified assemblies to the scanning list.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan.</param>
    IAssemblyScanBuilder FromAssemblies(params Assembly[] assemblies);

    /// <summary>
    /// Filters the types to be registered using a predicate.
    /// </summary>
    /// <param name="predicate">A function to test each type; if true, the type is included.</param>
    IAssemblyScanBuilder AddClasses(Func<Type, bool>? predicate = null);

    /// <summary>
    /// Registers the found types as their implemented interfaces.
    /// </summary>
    IAssemblyScanBuilder AsImplementedInterfaces();

    /// <summary>
    /// Registers the found types as themselves.
    /// </summary>
    IAssemblyScanBuilder AsSelf();

    /// <summary>
    /// Specifies the lifetime for the registered services.
    /// </summary>
    /// <param name="lifetime">The <see cref="ServiceLifetime"/> to use.</param>
    IAssemblyScanBuilder WithLifetime(ServiceLifetime lifetime);
}

/// <summary>
/// Convenience extensions for setting service lifetimes in a fluent way.
/// </summary>
public static class IAssemblyScanBuilderExtensions {
    /// <summary>
    /// Sets the lifetime to <see cref="ServiceLifetime.Singleton"/>.
    /// </summary>
    public static IAssemblyScanBuilder WithSingletonLifetime(this IAssemblyScanBuilder builder) {
        return builder.WithLifetime(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Sets the lifetime to <see cref="ServiceLifetime.Scoped"/>.
    /// </summary>
    public static IAssemblyScanBuilder WithScopedLifetime(this IAssemblyScanBuilder builder) {
        return builder.WithLifetime(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Sets the lifetime to <see cref="ServiceLifetime.Transient"/>.
    /// </summary>
    public static IAssemblyScanBuilder WithTransientLifetime(this IAssemblyScanBuilder builder) {
        return builder.WithLifetime(ServiceLifetime.Transient);
    }
}

internal class AssemblyScanBuilder : IAssemblyScanBuilder {
    private readonly IServiceCollection _services;
    private readonly List<Assembly> _assemblies = [];
    private Func<Type, bool> _predicate = _ => true;
    private bool _asImplementedInterfaces = false;
    private ServiceLifetime _lifetime = ServiceLifetime.Scoped;

    public AssemblyScanBuilder(IServiceCollection services) {
        this._services = services;
    }

    public IAssemblyScanBuilder FromAssemblyOf<T>() {
        this._assemblies.Add(typeof(T).Assembly);
        return this;
    }

    public IAssemblyScanBuilder FromAssemblies(params Assembly[] assemblies) {
        this._assemblies.AddRange(assemblies);
        return this;
    }

    public IAssemblyScanBuilder AddClasses(Func<Type, bool>? predicate = null) {
        if(predicate != null) this._predicate = predicate;
        return this;
    }

    public IAssemblyScanBuilder AsImplementedInterfaces() {
        this._asImplementedInterfaces = true;
        return this;
    }

    public IAssemblyScanBuilder AsSelf() {
        this._asImplementedInterfaces = false;
        return this;
    }

    public IAssemblyScanBuilder WithLifetime(ServiceLifetime lifetime) {
        this._lifetime = lifetime;
        return this;
    }

    /// <summary>
    /// Executes the scanning and registration logic.
    /// </summary>
    internal void Register() {
        var types = this._assemblies
            .SelectMany(a => a.GetExportedTypes())
            .Where(t => t.IsClass && !t.IsAbstract && this._predicate(t));

        foreach(var implementationType in types) {
            if(this._asImplementedInterfaces) {
                var interfaces = implementationType.GetInterfaces();
                foreach(var interfaceType in interfaces) {
                    this._services.TryAdd(new ServiceDescriptor(interfaceType, implementationType, this._lifetime));
                }
            }
            else {
                this._services.TryAdd(new ServiceDescriptor(implementationType, implementationType, this._lifetime));
            }
        }
    }
}