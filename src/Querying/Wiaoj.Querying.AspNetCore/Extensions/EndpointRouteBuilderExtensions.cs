using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.Querying;
using Wiaoj.Querying.AspNetCore;

#pragma warning disable IDE0130
namespace Microsoft.AspNetCore.Builder;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring query validation endpoint filters on route handlers and groups.
/// </summary>
public static class EndpointRouteBuilderExtensions {
    /// <summary>
    /// Adds automatic query schema validation to the route handler by resolving <see cref="QuerySchema{TEntity}"/>
    /// from the application's dependency injection container.
    /// </summary>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    public static RouteHandlerBuilder WithQueryValidation<TEntity>(this RouteHandlerBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.AddEndpointFilterFactory((filterFactoryContext, next) => {
            QuerySchema<TEntity> schema = filterFactoryContext.ApplicationServices.GetService<QuerySchema<TEntity>>()
                ?? throw new InvalidOperationException(
                    $"No QuerySchema<{typeof(TEntity).Name}> was registered in the dependency injection container. " +
                    $"Ensure you have registered it via services.AddQuerying().AddSchema<{typeof(TEntity).Name}, YourSchema>() or similar.");

            QueryValidationEndpointFilter<TEntity> filter = new(schema);
            return (context) => filter.InvokeAsync(context, next);
        });

        return builder;
    }

    /// <summary>
    /// Adds automatic query schema validation to a route group by resolving <see cref="QuerySchema{TEntity}"/>
    /// from the application's dependency injection container.
    /// </summary>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The route group builder for method chaining.</returns>
    public static RouteGroupBuilder WithQueryValidation<TEntity>(this RouteGroupBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.AddEndpointFilterFactory((filterFactoryContext, next) => {
            QuerySchema<TEntity> schema = filterFactoryContext.ApplicationServices.GetService<QuerySchema<TEntity>>()
                ?? throw new InvalidOperationException(
                    $"No QuerySchema<{typeof(TEntity).Name}> was registered in the dependency injection container. " +
                    $"Ensure you have registered it via services.AddQuerying().AddSchema<{typeof(TEntity).Name}, YourSchema>() or similar.");

            QueryValidationEndpointFilter<TEntity> filter = new(schema);
            return (context) => filter.InvokeAsync(context, next);
        });

        return builder;
    }

    /// <summary>
    /// Adds automatic query schema validation to the endpoint using an explicitly specified <see cref="QuerySchema{T}"/>.
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