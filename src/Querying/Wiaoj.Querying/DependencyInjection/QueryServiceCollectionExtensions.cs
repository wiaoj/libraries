using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Querying;
using Wiaoj.Querying.DependencyInjection;
using Wiaoj.Querying.Parsers;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for setting up query engine infrastructure in an <see cref="IServiceCollection"/>.
/// </summary>
public static class QueryServiceCollectionExtensions {
    /// <summary>
    /// Adds core query engine infrastructure, default payload parsers (<see cref="JsonQueryPayloadParser"/>, <see cref="BracketQueryPayloadParser"/>),
    /// and returns a builder for fluent configuration.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <returns>An <see cref="IQueryingBuilder"/> to configure schemas, options, and parsers.</returns>
    public static IQueryingBuilder AddQuerying(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.AddOptions<QueryOptions>();

        // Register default payload parsers using TryAddEnumerable to prevent duplicates
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueryPayloadParser, JsonQueryPayloadParser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IQueryPayloadParser, BracketQueryPayloadParser>());

        return new QueryingBuilder(services);
    }

    /// <summary>
    /// Adds core query engine infrastructure, default payload parsers, and executes the specified builder configuration action.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">The configuration delegate to set up schemas, options, and payload parsers.</param>
    /// <returns>An <see cref="IQueryingBuilder"/> instance for fluent chaining.</returns>
    public static IQueryingBuilder AddQuerying(this IServiceCollection services, Action<IQueryingBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        IQueryingBuilder builder = services.AddQuerying();
        configure(builder);

        return builder;
    }
}