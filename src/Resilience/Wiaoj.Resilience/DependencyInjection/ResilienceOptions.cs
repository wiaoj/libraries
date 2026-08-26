namespace Wiaoj.Resilience;

/// <summary>
/// Root configuration options for the resilience engine containing circuit breaker and timeout policy registrations.
/// </summary>
public sealed class ResilienceOptions {
    /// <summary>
    /// Gets the registered circuit breaker policy factories indexed by policy name.
    /// </summary>
    public Dictionary<string, Func<IServiceProvider, ICircuitBreaker>> Policies { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the fallback default circuit breaker factory delegate.
    /// </summary>
    public Func<IServiceProvider, ICircuitBreaker>? DefaultPolicy { get; set; }

    /// <summary>
    /// Gets the registered timeout policy factories indexed by policy name.
    /// </summary>
    public Dictionary<string, Func<IServiceProvider, ITimeoutStrategy>> TimeoutPolicies { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the fallback default timeout strategy factory delegate.
    /// </summary>
    public Func<IServiceProvider, ITimeoutStrategy>? DefaultTimeoutPolicy { get; set; }
}