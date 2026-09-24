using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Resilience;
using Wiaoj.Resilience.DependencyInjection;
using Wiaoj.Resilience.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for setting up resilience services in an <see cref="IServiceCollection"/>.
/// </summary>
public static class ResilienceServiceCollectionExtensions {
    /// <summary>
    /// Adds resilience infrastructure and returns a builder to register circuit breaker and timeout policies on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The resilience builder.</returns>
    public static IResilienceBuilder AddWiaojResilience(this IServiceCollection services) {
        Preca.ThrowIfNull(services);

        services.AddOptions<ResilienceOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICircuitBreakerFactory, DefaultCircuitBreakerFactory>();
        services.TryAddTransient(typeof(ICircuitBreaker<>), typeof(TypedCircuitBreakerWrapper<>)); 
        services.TryAddSingleton<ITimeoutStrategyFactory, DefaultTimeoutStrategyFactory>(); 
        services.TryAddTransient(typeof(ITimeoutStrategy<>), typeof(TypedTimeoutStrategyWrapper<>));

        return new ResilienceBuilder(services);
    }

    /// <summary>
    /// Adds resilience infrastructure and configures circuit breaker and timeout policies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Registers the policies on the builder.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddWiaojResilience(
        this IServiceCollection services,
        Action<IResilienceBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        configure(services.AddWiaojResilience());
        return services;
    }
}