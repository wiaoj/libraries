using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting;
using Wiaoj.RateLimiting.DependencyInjection;
using Wiaoj.RateLimiting.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for setting up rate limiting services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class RateLimitingServiceCollectionExtensions {
    /// <summary>
    /// Adds rate limiting infrastructure and returns a builder to register policies on.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <returns>The rate limiting builder.</returns>
    public static IRateLimitingBuilder AddWiaojRateLimiting(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.AddOptions<RateLimitingOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRateLimiter, DefaultRateLimiter>();
        services.TryAddTransient(typeof(IRateLimiter<>), typeof(TypedRateLimiterWrapper<>));

        return new RateLimitingBuilder(services);
    }

    /// <summary>
    /// Adds rate limiting infrastructure and configures policies via a builder action.
    /// </summary>
    /// <param name="services">The target service collection.</param>
    /// <param name="configure">The builder configuration action.</param>
    /// <returns>The service collection instance for chaining.</returns>
    public static IServiceCollection AddWiaojRateLimiting(
        this IServiceCollection services,
        Action<IRateLimitingBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        configure(services.AddWiaojRateLimiting());
        return services;
    }
}