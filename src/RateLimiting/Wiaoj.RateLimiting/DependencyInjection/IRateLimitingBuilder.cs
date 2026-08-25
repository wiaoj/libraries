using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.RateLimiting;

/// <summary>
/// Root builder for registering and configuring rate limiting policies.
/// </summary>
public interface IRateLimitingBuilder {
    /// <summary>
    /// Gets the application service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers a named rate limiting policy using a configuration action.
    /// </summary>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="configure">The policy configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IRateLimitingBuilder AddPolicy(string policyName, Action<IRateLimitPolicyBuilder> configure);

    /// <summary>
    /// Registers a strongly-typed rate limiting policy using a configuration action.
    /// </summary>
    /// <typeparam name="TPolicy">The marker type representing the policy category.</typeparam>
    /// <param name="configure">The policy configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IRateLimitingBuilder AddPolicy<TPolicy>(Action<IRateLimitPolicyBuilder> configure) where TPolicy : notnull;

    /// <summary>
    /// Configures the default fallback policy used when no policy name is specified.
    /// </summary>
    /// <param name="configure">The policy configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    IRateLimitingBuilder UseDefaultPolicy(Action<IRateLimitPolicyBuilder> configure);
}