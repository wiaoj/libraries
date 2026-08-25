using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A minimal fluent builder contract for configuring a specific rate limit policy.
/// </summary>
public interface IRateLimitPolicyBuilder {
    /// <summary>
    /// Gets the application service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Gets the name of the policy being configured.
    /// </summary>
    string PolicyName { get; }

    /// <summary>
    /// Registers the core algorithm factory delegate for this policy.
    /// </summary>
    /// <param name="factory">The algorithm factory delegate.</param>
    /// <returns>The policy builder instance for chaining.</returns>
    IRateLimitPolicyBuilder UseAlgorithm(Func<IServiceProvider, IRateLimitAlgorithm> factory);

    /// <summary>
    /// Appends a decorator wrapper delegate to this policy's execution pipeline (e.g. Fail-Open, Negative Caching).
    /// </summary>
    /// <param name="decorator">The decorator factory delegate.</param>
    /// <returns>The policy builder instance for chaining.</returns>
    IRateLimitPolicyBuilder AddDecorator(Func<IServiceProvider, IRateLimitAlgorithm, IRateLimitAlgorithm> decorator);
}