using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Resilience.DependencyInjection;

/// <summary>
/// Root builder contract for registering circuit breaker policies.
/// </summary>
public interface IResilienceBuilder {
    /// <summary>Gets the application service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>Registers a consecutive failures circuit breaker policy by name.</summary>
    IResilienceBuilder AddConsecutiveBreaker(string policyName, Action<CircuitBreakerOptions> configure);

    /// <summary>Registers a consecutive failures circuit breaker policy with a strongly-typed tag.</summary>
    IResilienceBuilder AddConsecutiveBreaker<TPolicy>(Action<CircuitBreakerOptions> configure) where TPolicy : notnull;

    /// <summary>Registers a percentage-based sampling window circuit breaker policy by name.</summary>
    IResilienceBuilder AddSamplingBreaker(string policyName, Action<SamplingWindowCircuitBreakerOptions> configure);

    /// <summary>Registers a percentage-based sampling window circuit breaker policy with a strongly-typed tag.</summary>
    IResilienceBuilder AddSamplingBreaker<TPolicy>(Action<SamplingWindowCircuitBreakerOptions> configure) where TPolicy : notnull;

    /// <summary>Configures the default consecutive failures circuit breaker.</summary>
    IResilienceBuilder UseDefaultConsecutiveBreaker(Action<CircuitBreakerOptions> configure);

    /// <summary>Configures the default sampling window circuit breaker.</summary>
    IResilienceBuilder UseDefaultSamplingBreaker(Action<SamplingWindowCircuitBreakerOptions> configure);
}