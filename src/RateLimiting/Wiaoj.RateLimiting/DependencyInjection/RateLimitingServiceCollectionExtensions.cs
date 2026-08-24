using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.DependencyInjection;

#pragma warning disable IDE0130 // Namespace matches standard DI convention
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for setting up rate limiting services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class RateLimitingServiceCollectionExtensions {
    /// <summary>
    /// Adds rate limiting services and allows configuring the rate limiting algorithm.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">The builder action to select and configure an algorithm.</param>
    /// <returns>The original <see cref="IServiceCollection"/>.</returns>
    public static IServiceCollection AddWiaojRateLimiting(
        this IServiceCollection services,
        Action<IRateLimitingBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        services.TryAddSingleton(TimeProvider.System);

        RateLimitingBuilder builder = new(services);
        configure(builder);

        return services;
    }
}