using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using Tyto;
using Wiaoj.Ddd;
using Wiaoj.Ddd.Tyto;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
/// <summary>
/// Extension methods for configuring Tyto integration within the DDD architecture.
/// </summary>
public static class DddBuilderExtensions {
    /// <summary>
    /// Scans the specified assemblies for <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementations
    /// and registers them to automatically publish events via Tyto.
    /// Uses <see cref="ServiceLifetime.Scoped"/> by default.
    /// </summary>
    /// <param name="builder">The DDD builder instance.</param>
    /// <param name="assemblies">The assemblies to scan for mappers.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="assemblies"/> is null.</exception>
    public static IDddBuilder AddTytoIntegration(this IDddBuilder builder, params IEnumerable<Assembly> assemblies) {
        return builder.AddTytoIntegration(ServiceLifetime.Scoped, assemblies);
    }

    /// <summary>
    /// Scans the specified assemblies for <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementations
    /// and registers them with the specified service lifetime.
    /// </summary>
    /// <param name="builder">The DDD builder instance.</param>
    /// <param name="lifetime">The lifetime of the registered services (Scoped, Singleton, or Transient).</param>
    /// <param name="assemblies">The assemblies to scan for mappers.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="assemblies"/> is null.</exception>
    public static IDddBuilder AddTytoIntegration(this IDddBuilder builder,
                                                 ServiceLifetime lifetime,
                                                 params IEnumerable<Assembly> assemblies) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(assemblies);
        ScanAndRegisterMappers(builder, assemblies, lifetime);
        return builder;
    }

    /// <summary>
    /// Scans the assembly containing the specified marker type for <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementations.
    /// </summary>
    /// <typeparam name="TMarker">A type located in the assembly to scan.</typeparam>
    /// <param name="builder">The DDD builder instance.</param>
    /// <param name="lifetime">The lifetime of the registered services. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static IDddBuilder AddTytoIntegration<TMarker>(this IDddBuilder builder) {
        Preca.ThrowIfNull(builder);

        return builder.AddTytoIntegration(ServiceLifetime.Scoped, typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Scans the assembly containing the specified marker type for <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementations.
    /// </summary>
    /// <typeparam name="TMarker">A type located in the assembly to scan.</typeparam>
    /// <param name="builder">The DDD builder instance.</param>
    /// <param name="lifetime">The lifetime of the registered services. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    public static IDddBuilder AddTytoIntegration<TMarker>(this IDddBuilder builder, ServiceLifetime lifetime) {
        Preca.ThrowIfNull(builder);

        return builder.AddTytoIntegration(lifetime, typeof(TMarker).Assembly);
    }

    /// <summary>
    /// Registers a specific <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementation manually
    /// using the default <see cref="ServiceLifetime.Scoped"/> lifetime.
    /// </summary>
    /// <typeparam name="TMapper">The mapper type to register. Must be a concrete class.</typeparam>
    /// <param name="builder">The DDD builder instance.</param>
    /// <returns>The builder instance for chaining.</returns>
    public static IDddBuilder AddTytoIntegrationMapper<TMapper>(this IDddBuilder builder) where TMapper : class {
        Preca.ThrowIfNull(builder);
        return builder.AddTytoIntegrationMapper<TMapper>(ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Registers a specific <see cref="IIntegrationEventMapper{TDomainEvent, TIntegrationEvent}"/> implementation manually.
    /// This method validates that the type implements the interface and targets a valid Tyto IEvent.
    /// </summary>
    /// <typeparam name="TMapper">The mapper type to register. Must be a concrete class.</typeparam>
    /// <param name="builder">The DDD builder instance.</param>
    /// <param name="lifetime">The lifetime of the registered service. Defaults to <see cref="ServiceLifetime.Scoped"/>.</param>
    /// <returns>The builder instance for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <typeparamref name="TMapper"/> is abstract, an interface, does not implement the mapper interface, 
    /// or maps to a type that does not implement <see cref="IEvent"/>.
    /// </exception>
    public static IDddBuilder AddTytoIntegrationMapper<TMapper>(this IDddBuilder builder, ServiceLifetime lifetime)
        where TMapper : class {
        Preca.ThrowIfNull(builder);

        Type mapperType = typeof(TMapper);

        // Validation 1: Must be concrete
        Preca.ThrowIf(
            mapperType.IsAbstract || mapperType.IsInterface,
            ()=> new InvalidOperationException($"Type '{mapperType.Name}' cannot be abstract or an interface."));

        // Validation 2: Check if it implements any mapper interface
        Type openMapperType = typeof(IIntegrationEventMapper<,>);
        bool isMapper = mapperType.GetInterfaces()
            .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openMapperType);

        Preca.ThrowIfFalse(
            isMapper, 
            ()=> new InvalidOperationException($"Type '{mapperType.Name}' does not implement 'IIntegrationEventMapper<TDomainEvent, TIntegrationEvent>'."));

        // Attempt registration and check count
        int registeredCount = RegisterMapperType(builder, mapperType, lifetime);

        // Validation 3: Ensure at least one valid mapping was found (Tyto IEvent check happens inside RegisterMapperType)
        Preca.ThrowIfZero(
            registeredCount, 
            () => new InvalidOperationException($"Type '{mapperType.Name}' implements the mapper interface, but the target integration event type does not implement Tyto's 'IEvent' interface."));

        return builder;
    }

    // --- Internal Helpers ---

    private static void ScanAndRegisterMappers(IDddBuilder builder, IEnumerable<Assembly> assemblies, ServiceLifetime lifetime) {
        Type openMapperType = typeof(IIntegrationEventMapper<,>);

        foreach(Assembly assembly in assemblies) {
            // Select only concrete classes (no abstracts, no interfaces, no open generics)
            IEnumerable<Type> types = assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.IsGenericTypeDefinition);

            foreach(Type type in types) {
                // Check if the type implements IIntegrationEventMapper<,>
                if(type.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == openMapperType)) {
                    RegisterMapperType(builder, type, lifetime);
                }
            }
        }
    }

    /// <summary>
    /// Registers the mapper and its corresponding auto-publish handler.
    /// Returns the number of successful registrations (useful for validation).
    /// </summary>
    private static int RegisterMapperType(IDddBuilder builder, Type mapperType, ServiceLifetime lifetime) {
        Type openMapperType = typeof(IIntegrationEventMapper<,>);
        int registrationCount = 0;

        // Find all interfaces implemented by the mapper that match IIntegrationEventMapper<,>
        // A single class might implement multiple mappings.
        IEnumerable<Type> interfaces = mapperType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == openMapperType);

        foreach(Type? interfaceType in interfaces) {
            // Extract generic arguments: <TDomainEvent, TIntegrationEvent>
            Type[] arguments = interfaceType.GetGenericArguments();
            Type domainEventType = arguments[0];
            Type integrationEventType = arguments[1];

            // CRITICAL CHECK: Does the target type implement Tyto's "IEvent" interface?
            // If not, we cannot use Tyto to publish it.
            if(!typeof(IEvent).IsAssignableFrom(integrationEventType)) {
                continue;
            }

            // 1. Register the Mapper itself with the specified lifetime
            // Using ServiceDescriptor.Describe allows passing the lifetime dynamically.
            // Note: If the mapper implements multiple interfaces, the implementation is registered for each interface.
            builder.Services.TryAdd(ServiceDescriptor.Describe(interfaceType, mapperType, lifetime));

            // 2. Register the AutoPublishIntegrationEventHandler
            // This is the bridge that listens to Domain Events and publishes Integration Events.

            // Generic Type: AutoPublishIntegrationEventHandler<DomainEvent, IntegrationEvent>
            Type implementationType = typeof(AutoPublishIntegrationEventHandler<,>)
                .MakeGenericType(domainEventType, integrationEventType);

            // Service Type: IPostDomainEventHandler<DomainEvent>
            Type serviceType = typeof(IPostDomainEventHandler<>)
                .MakeGenericType(domainEventType);

            // 3. Register the Handler
            // We use TryAddEnumerable because multiple handlers might exist for the same Domain Event 
            // (e.g., one for logging, one for integration).
            builder.Services.TryAddEnumerable(ServiceDescriptor.Describe(serviceType, implementationType, lifetime));

            registrationCount++;
        }

        return registrationCount;
    }
}