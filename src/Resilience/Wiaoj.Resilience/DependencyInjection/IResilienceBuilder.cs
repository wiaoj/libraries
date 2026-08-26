using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Resilience;

/// <summary>
/// Root builder contract for registering circuit breaker and timeout policies.
/// </summary>
public interface IResilienceBuilder {
    /// <summary>Gets the application service collection.</summary>
    IServiceCollection Services { get; }

    /// <summary>Registers a named circuit breaker policy factory.</summary>
    IResilienceBuilder AddPolicy(string policyName, Func<IServiceProvider, ICircuitBreaker> factory);

    /// <summary>Configures the default fallback circuit breaker policy factory.</summary>
    IResilienceBuilder UseDefaultPolicy(Func<IServiceProvider, ICircuitBreaker> factory);

    /// <summary>Registers a named timeout policy factory.</summary>
    IResilienceBuilder AddTimeoutPolicy(string policyName, Func<IServiceProvider, ITimeoutStrategy> factory);

    /// <summary>Configures the default fallback timeout policy factory.</summary>
    IResilienceBuilder UseDefaultTimeoutPolicy(Func<IServiceProvider, ITimeoutStrategy> factory);
}