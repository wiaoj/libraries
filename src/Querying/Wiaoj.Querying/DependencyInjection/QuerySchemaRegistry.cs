using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wiaoj.Querying.DependencyInjection;

/// <summary>
/// Records which schemas were registered for each entity, so the entity-keyed <c>QuerySchema&lt;TEntity&gt;</c> can
/// refuse to guess when there is more than one.
/// </summary>
/// <remarks>
/// <para>
/// A schema is an endpoint's query contract, and two endpoints over one entity can expose different surfaces — an
/// admin listing that filters by owner, a public one that must not. Both register against the same entity. The
/// entity-keyed service used to be added with <c>TryAddSingleton</c>, so the second registration was dropped without
/// a word and every endpoint resolving by entity accepted the first schema's filters.
/// </para>
/// <para>
/// Class schemas may be registered side by side and are selected by type. An inline or instance schema has no type
/// to select it by, so it must be the only schema for its entity; anything else throws at registration.
/// </para>
/// </remarks>
internal sealed class QuerySchemaRegistry {
    private readonly Dictionary<Type, List<Type>> _schemaTypesByEntity = [];
    private readonly HashSet<Type> _entitiesWithUntypedSchema = [];

    /// <summary>Gets the registry for <paramref name="services"/>, adding it on first use.</summary>
    public static QuerySchemaRegistry For(IServiceCollection services) {
        foreach(ServiceDescriptor descriptor in services) {
            if(descriptor.ServiceType == typeof(QuerySchemaRegistry) && descriptor.ImplementationInstance is QuerySchemaRegistry existing) {
                return existing;
            }
        }

        QuerySchemaRegistry registry = new();
        services.AddSingleton(registry);
        return registry;
    }

    /// <summary>Records a schema class, and registers the entity-keyed service the first time the entity is seen.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="entityType">The entity the schema describes.</param>
    /// <param name="serviceType"><c>QuerySchema&lt;TEntity&gt;</c>, supplied by the caller so nothing is constructed at run time.</param>
    /// <param name="schemaType">The schema class.</param>
    public void AddTyped(IServiceCollection services, Type entityType, Type serviceType, Type schemaType) {
        if(this._entitiesWithUntypedSchema.Contains(entityType)) {
            throw MixedRegistration(entityType, schemaType);
        }

        if(!this._schemaTypesByEntity.TryGetValue(entityType, out List<Type>? schemaTypes)) {
            schemaTypes = [];
            this._schemaTypesByEntity[entityType] = schemaTypes;
            services.TryAdd(ServiceDescriptor.Singleton(serviceType, provider => this.ResolveDefault(provider, entityType)));
        }

        if(!schemaTypes.Contains(schemaType)) {
            schemaTypes.Add(schemaType);
        }
    }

    /// <summary>Records an inline or instance schema, which must be the only schema for its entity.</summary>
    public void AddUntyped(Type entityType) {
        if(this._entitiesWithUntypedSchema.Contains(entityType)) {
            throw new InvalidOperationException(
                $"A QuerySchema<{entityType.Name}> was already registered inline or as an instance. A second one would " +
                "have nothing to select it by, and the first would silently win. To expose different query surfaces over " +
                $"{entityType.Name}, declare each as a class deriving from QuerySchema<{entityType.Name}>, register them with " +
                $"AddSchema<{entityType.Name}, TSchema>(), and select one per endpoint with WithQueryValidation<{entityType.Name}, TSchema>().");
        }

        if(this._schemaTypesByEntity.TryGetValue(entityType, out List<Type>? schemaTypes)) {
            throw MixedRegistration(entityType, schemaTypes[0]);
        }

        this._entitiesWithUntypedSchema.Add(entityType);
    }

    private object ResolveDefault(IServiceProvider provider, Type entityType) {
        List<Type> schemaTypes = this._schemaTypesByEntity[entityType];

        if(schemaTypes.Count == 1) {
            return provider.GetRequiredService(schemaTypes[0]);
        }

        throw new InvalidOperationException(
            $"{schemaTypes.Count} query schemas are registered for {entityType.Name} " +
            $"({string.Join(", ", schemaTypes.Select(type => type.Name))}), so QuerySchema<{entityType.Name}> does not say which " +
            $"one applies. Select one per endpoint with WithQueryValidation<{entityType.Name}, TSchema>(), or inject the " +
            "schema class itself.");
    }

    private static InvalidOperationException MixedRegistration(Type entityType, Type schemaType) {
        return new InvalidOperationException(
            $"{entityType.Name} has both a schema class ({schemaType.Name}) and an inline or instance schema. The untyped " +
            $"one cannot be selected per endpoint, and would be indistinguishable from the class when resolving " +
            $"QuerySchema<{entityType.Name}>. Declare every schema for {entityType.Name} as a class.");
    }
}
