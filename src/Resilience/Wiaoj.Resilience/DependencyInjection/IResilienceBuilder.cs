using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Resilience;

/// <summary>
/// Root builder contract for registering circuit breaker policies.
/// </summary>
public interface IResilienceBuilder {
    /// <summary>Gets the application service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers a named circuit breaker policy factory.
    /// </summary>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="factory">The circuit breaker factory delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IResilienceBuilder AddPolicy(string policyName, Func<IServiceProvider, ICircuitBreaker> factory);

    /// <summary>
    /// Configures the default fallback circuit breaker policy factory.
    /// </summary>
    /// <param name="factory">The circuit breaker factory delegate.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IResilienceBuilder UseDefaultPolicy(Func<IServiceProvider, ICircuitBreaker> factory);
}