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

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));



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
    /// Adds automatic query schema validation to the route handler with custom endpoint options by resolving <see cref="QuerySchema{TEntity}"/>
    /// from the application's dependency injection container.
    /// </summary>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="configure">An action to configure endpoint query validation and parameter options.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    public static RouteHandlerBuilder WithQueryValidation<TEntity>(
        this RouteHandlerBuilder builder,
        Action<QueryValidationEndpointOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        QueryValidationEndpointOptions options = new();
        configure(options);
        ConfigureEndpointOptions(builder, opt => CopyOptions(options, opt));

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));



        builder.AddEndpointFilterFactory((filterFactoryContext, next) => {
            QuerySchema<TEntity> schema = filterFactoryContext.ApplicationServices.GetService<QuerySchema<TEntity>>()
                ?? throw new InvalidOperationException(
                    $"No QuerySchema<{typeof(TEntity).Name}> was registered in the dependency injection container. " +
                    $"Ensure you have registered it via services.AddQuerying().AddSchema<{typeof(TEntity).Name}, YourSchema>() or similar.");

            QueryValidationEndpointFilter<TEntity> filter = new(schema, options);
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

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));



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
    /// Adds automatic query schema validation to a route group with custom endpoint options by resolving <see cref="QuerySchema{TEntity}"/>
    /// from the application's dependency injection container.
    /// </summary>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The route group builder.</param>
    /// <param name="configure">An action to configure endpoint query validation and parameter options.</param>
    /// <returns>The route group builder for method chaining.</returns>
    public static RouteGroupBuilder WithQueryValidation<TEntity>(
        this RouteGroupBuilder builder,
        Action<QueryValidationEndpointOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        QueryValidationEndpointOptions options = new();
        configure(options);
        ConfigureEndpointOptions(builder, opt => CopyOptions(options, opt));

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));



        builder.AddEndpointFilterFactory((filterFactoryContext, next) => {
            QuerySchema<TEntity> schema = filterFactoryContext.ApplicationServices.GetService<QuerySchema<TEntity>>()
                ?? throw new InvalidOperationException(
                    $"No QuerySchema<{typeof(TEntity).Name}> was registered in the dependency injection container. " +
                    $"Ensure you have registered it via services.AddQuerying().AddSchema<{typeof(TEntity).Name}, YourSchema>() or similar.");

            QueryValidationEndpointFilter<TEntity> filter = new(schema, options);
            return (context) => filter.InvokeAsync(context, next);
        });

        return builder;
    }

    /// <summary>
    /// Validates the endpoint's query against the schema class <typeparamref name="TSchema"/>, resolved from the container.
    /// </summary>
    /// <typeparam name="TEntity">The entity type queried.</typeparam>
    /// <typeparam name="TSchema">The schema class that is this endpoint's query contract.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// A filter surface is part of what an endpoint promises, so two endpoints over one entity can need different
    /// schemas — an admin listing filtering by owner, a public one that must not. Register both with
    /// <c>AddSchema&lt;TEntity, TSchema&gt;()</c> and select one here. Validation, <c>Query&lt;TEntity&gt;</c> binding and
    /// the OpenAPI document all use the schema selected, so they cannot describe different contracts.
    /// </para>
    /// <para>
    /// <c>WithQueryValidation&lt;TEntity&gt;()</c> keeps working while the entity has one schema, and throws once it has
    /// more instead of picking one.
    /// </para>
    /// </remarks>
    public static RouteHandlerBuilder WithQueryValidation<TEntity, TSchema>(this RouteHandlerBuilder builder)
        where TSchema : QuerySchema<TEntity> {
        Preca.ThrowIfNull(builder);
        return AddSelectedSchemaValidation<RouteHandlerBuilder, TEntity, TSchema>(builder, configure: null);
    }

    /// <summary>
    /// Validates the endpoint's query against the schema class <typeparamref name="TSchema"/>, with endpoint options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type queried.</typeparam>
    /// <typeparam name="TSchema">The schema class that is this endpoint's query contract.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <param name="configure">Configures endpoint query validation and parameter options.</param>
    /// <returns>The route handler builder for method chaining.</returns>
    public static RouteHandlerBuilder WithQueryValidation<TEntity, TSchema>(
        this RouteHandlerBuilder builder,
        Action<QueryValidationEndpointOptions> configure) where TSchema : QuerySchema<TEntity> {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        return AddSelectedSchemaValidation<RouteHandlerBuilder, TEntity, TSchema>(builder, configure);
    }

    /// <summary>
    /// Validates every endpoint in the group against the schema class <typeparamref name="TSchema"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type queried.</typeparam>
    /// <typeparam name="TSchema">The schema class that is the group's query contract.</typeparam>
    /// <param name="builder">The route group builder.</param>
    /// <returns>The route group builder for method chaining.</returns>
    public static RouteGroupBuilder WithQueryValidation<TEntity, TSchema>(this RouteGroupBuilder builder)
        where TSchema : QuerySchema<TEntity> {
        Preca.ThrowIfNull(builder);
        return AddSelectedSchemaValidation<RouteGroupBuilder, TEntity, TSchema>(builder, configure: null);
    }

    /// <summary>
    /// Validates every endpoint in the group against the schema class <typeparamref name="TSchema"/>, with endpoint options.
    /// </summary>
    /// <typeparam name="TEntity">The entity type queried.</typeparam>
    /// <typeparam name="TSchema">The schema class that is the group's query contract.</typeparam>
    /// <param name="builder">The route group builder.</param>
    /// <param name="configure">Configures endpoint query validation and parameter options.</param>
    /// <returns>The route group builder for method chaining.</returns>
    public static RouteGroupBuilder WithQueryValidation<TEntity, TSchema>(
        this RouteGroupBuilder builder,
        Action<QueryValidationEndpointOptions> configure) where TSchema : QuerySchema<TEntity> {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);
        return AddSelectedSchemaValidation<RouteGroupBuilder, TEntity, TSchema>(builder, configure);
    }

    private static TBuilder AddSelectedSchemaValidation<TBuilder, TEntity, TSchema>(
        TBuilder builder,
        Action<QueryValidationEndpointOptions>? configure)
        where TBuilder : IEndpointConventionBuilder
        where TSchema : QuerySchema<TEntity> {

        QueryValidationEndpointOptions? options = null;
        if(configure is not null) {
            options = new QueryValidationEndpointOptions();
            configure(options);
            QueryValidationEndpointOptions captured = options;
            ConfigureEndpointOptions(builder, opt => CopyOptions(captured, opt));
        }

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity), typeof(TSchema)));

        builder.AddEndpointFilterFactory((filterFactoryContext, next) => {
            TSchema schema = filterFactoryContext.ApplicationServices.GetService<TSchema>()
                ?? throw new InvalidOperationException(
                    $"No {typeof(TSchema).Name} was registered in the dependency injection container. " +
                    $"Register it with services.AddQuerying().AddSchema<{typeof(TEntity).Name}, {typeof(TSchema).Name}>().");

            QueryValidationEndpointFilter<TEntity> filter = new(schema, options);
            return context => filter.InvokeAsync(context, next);
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

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));


        builder.AddEndpointFilter(new QueryValidationEndpointFilter<TEntity>(schema));
        return builder;
    }

    /// <summary>
    /// Adds automatic query schema validation to the endpoint using an explicitly specified <see cref="QuerySchema{T}"/> and custom endpoint options.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <typeparam name="TEntity">The entity type of the query schema.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="schema">The query schema defining rules and limits to validate against.</param>
    /// <param name="configure">An action to configure endpoint query validation and parameter options.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder WithQueryValidation<TBuilder, TEntity>(
        this TBuilder builder,
        QuerySchema<TEntity> schema,
        Action<QueryValidationEndpointOptions> configure) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(schema);
        Preca.ThrowIfNull(configure);

        QueryValidationEndpointOptions options = new();
        configure(options);
        ConfigureEndpointOptions(builder, opt => CopyOptions(options, opt));

        builder.WithMetadata(new QueryValidationEndpointMetadata(typeof(TEntity)));


        builder.AddEndpointFilter(new QueryValidationEndpointFilter<TEntity>(schema, options));
        return builder;
    }

    /// <summary>
    /// Configures one or more parameter names to be ignored for this endpoint during query binding and validation.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="parameters">The parameter names to ignore.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder IgnoreQueryParameters<TBuilder>(
        this TBuilder builder,
        params ReadOnlySpan<string> parameters) where TBuilder : IEndpointConventionBuilder {
        string[] copy = parameters.ToArray();
        return ConfigureEndpointOptions(builder, options => options.IgnoreParameters(copy));
    }

    /// <summary>
    /// Configures parameter names to be ignored for this endpoint during query binding and validation.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="parameters">The collection of parameter names to ignore.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder IgnoreQueryParameters<TBuilder>(
        this TBuilder builder,
        IEnumerable<string> parameters) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(parameters);
        return ConfigureEndpointOptions(builder, options => options.IgnoreParameters(parameters));
    }

    /// <summary>
    /// Configures one or more parameter names to be explicitly allowed (un-ignored) for this endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="parameters">The parameter names to allow.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder AllowQueryParameters<TBuilder>(
        this TBuilder builder,
        params ReadOnlySpan<string> parameters) where TBuilder : IEndpointConventionBuilder {
        string[] copy = parameters.ToArray();
        return ConfigureEndpointOptions(builder, options => options.AllowParameters(copy));
    }

    /// <summary>
    /// Configures parameter names to be explicitly allowed (un-ignored) for this endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="parameters">The collection of parameter names to allow.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder AllowQueryParameters<TBuilder>(
        this TBuilder builder,
        IEnumerable<string> parameters) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(parameters);
        return ConfigureEndpointOptions(builder, options => options.AllowParameters(parameters));
    }

    /// <summary>
    /// Configures the endpoint to bypass all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder IgnoreGlobalQueryParameters<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder {
        return IgnoreGlobalQueryParameters(builder, true);
    }

    /// <summary>
    /// Configures whether the endpoint bypasses all globally configured ignored parameters from <see cref="QueryOptions"/>.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint builder.</param>
    /// <param name="ignore"><see langword="true"/> to ignore global parameters; otherwise, <see langword="false"/>.</param>
    /// <returns>The endpoint builder for method chaining.</returns>
    public static TBuilder IgnoreGlobalQueryParameters<TBuilder>(
        this TBuilder builder,
        bool ignore) where TBuilder : IEndpointConventionBuilder {
        return ConfigureEndpointOptions(builder, options => options.IgnoreGlobalParameters(ignore));
    }

    private static TBuilder ConfigureEndpointOptions<TBuilder>(
        TBuilder builder,
        Action<QueryValidationEndpointOptions> configure) where TBuilder : IEndpointConventionBuilder {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Add(endpointBuilder => {
            QueryValidationEndpointOptions? options = endpointBuilder.Metadata.OfType<QueryValidationEndpointOptions>().LastOrDefault();
            if(options is null) {
                options = new QueryValidationEndpointOptions();
                endpointBuilder.Metadata.Add(options);
            }
            configure(options);
        });

        return builder;
    }

    private static void CopyOptions(QueryValidationEndpointOptions source, QueryValidationEndpointOptions target) {
        target.IgnoresGlobalParameters = source.IgnoresGlobalParameters;
        foreach(string p in source.IgnoredParameters) target.IgnoredParameters.Add(p);
        foreach(string p in source.AllowedParameters) target.AllowedParameters.Add(p);
    }
}