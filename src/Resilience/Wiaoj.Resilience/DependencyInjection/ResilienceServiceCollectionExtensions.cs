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
    /// Adds resilience infrastructure and configures circuit breaker policies.
    /// </summary>
    public static IServiceCollection AddWiaojResilience(
        this IServiceCollection services,
        Action<IResilienceBuilder> configure) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configure);

        services.AddOptions<ResilienceOptions>();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ICircuitBreakerFactory, DefaultCircuitBreakerFactory>();
        services.TryAddTransient(typeof(ICircuitBreaker<>), typeof(TypedCircuitBreakerWrapper<>));

        ResilienceBuilder builder = new(services);
        configure(builder);

        return services;
    }
}