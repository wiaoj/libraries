using Microsoft.AspNetCore.Http;
using Wiaoj.Preconditions;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring query validation endpoint filters on route handlers.
/// </summary>
public static class EndpointRouteBuilderExtensions {
    /// <summary>
    /// Adds automatic query schema validation to the endpoint using a specified <see cref="QuerySchema{T}"/>.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="schema">The query schema defining rules and limits to validate against.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder WithQueryValidation<TBuilder, TEntity>(
        this TBuilder builder,
        QuerySchema<TEntity> schema) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(schema);

        builder.AddEndpointFilter(new QueryValidationEndpointFilter<TEntity>(schema));
        return builder;
    }
}