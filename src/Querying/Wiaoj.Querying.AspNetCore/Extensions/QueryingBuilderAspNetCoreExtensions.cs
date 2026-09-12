using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Wiaoj.Querying;
#pragma warning restore IDE0130

/// <summary>
/// ASP.NET Core-specific configuration for the query engine.
/// </summary>
public static class QueryingBuilderAspNetCoreExtensions {
    /// <summary>
    /// Names query fields with the same policy the application's JSON bodies use.
    /// </summary>
    /// <param name="builder">The query engine builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <remarks>
    /// <para>
    /// Takes <c>JsonOptions.SerializerOptions.PropertyNamingPolicy</c> — camelCase by default in a minimal API —
    /// and applies it to every schema's field names that were not named with <c>HasName</c>. A response that
    /// says <c>contentType</c> is then filtered with <c>contentType</c>, and a generated document says so.
    /// </para>
    /// <para>
    /// Opt-in rather than automatic: turning it on renames fields in the published document, which regenerates
    /// clients with different parameter names. The wire is unaffected — rendered names are added as aliases and
    /// the original names keep working — but the decision to change a published contract belongs to the
    /// application.
    /// </para>
    /// <para>
    /// A policy set explicitly with <c>UseFieldNamingPolicy</c> takes precedence.
    /// </para>
    /// </remarks>
    public static IQueryingBuilder UseJsonNamingPolicy(this IQueryingBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.AddOptions<QueryOptions>()
            .PostConfigure<IOptions<JsonOptions>>((query, json) =>
                query.FieldNamingPolicy ??= json.Value.SerializerOptions.PropertyNamingPolicy);

        return builder;
    }
}
