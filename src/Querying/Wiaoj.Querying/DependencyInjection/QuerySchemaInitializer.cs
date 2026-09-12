using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Wiaoj.Querying.DependencyInjection;

/// <summary>
/// Lets application-wide settings reach a schema without knowing its entity type.
/// </summary>
internal interface IQuerySchemaNaming {
    JsonNamingPolicy? FieldNamingPolicy { get; }

    void ApplyFieldNamingPolicy(JsonNamingPolicy policy);
}

/// <summary>
/// Applies application-wide query settings to a schema as the container hands it out.
/// </summary>
/// <remarks>
/// Every registration path goes through this — typed, inline, instance and assembly scanning — so a schema
/// injected into an RPC handler's constructor accepts the same names as one resolved by an HTTP binder.
/// </remarks>
internal static class QuerySchemaInitializer {
    public static TSchema Initialize<TSchema>(TSchema schema, IServiceProvider services) where TSchema : notnull {
        if(schema is IQuerySchemaNaming naming && naming.FieldNamingPolicy is null &&
           services.GetService<IOptions<QueryOptions>>()?.Value.FieldNamingPolicy is JsonNamingPolicy policy) {
            naming.ApplyFieldNamingPolicy(policy);
        }

        return schema;
    }
}
