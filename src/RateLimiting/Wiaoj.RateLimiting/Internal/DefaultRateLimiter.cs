using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting.Internal;

internal sealed class DefaultRateLimiter : IRateLimiter {
    private readonly IServiceProvider _serviceProvider;
    private readonly RateLimitingOptions _options;
    private readonly ConcurrentDictionary<string, IRateLimitAlgorithm> _resolvedPolicies = new(StringComparer.Ordinal);
    private readonly Lazy<IRateLimitAlgorithm?> _defaultPolicy;

    public DefaultRateLimiter(
        IServiceProvider serviceProvider,
        IOptions<RateLimitingOptions> options) {
        Preca.ThrowIfNull(serviceProvider);
        Preca.ThrowIfNull(options);

        this._serviceProvider = serviceProvider;
        this._options = options.Value;

        this._defaultPolicy = new Lazy<IRateLimitAlgorithm?>(() => {
            return this._options.DefaultPolicy?.Invoke(this._serviceProvider);
        });
    }

    public ValueTask<RateLimitDecision> TryAcquireAsync(
        string policyName,
        string key,
        int cost,
        CancellationToken cancellationToken) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        IRateLimitAlgorithm policy = GetPolicy(policyName);
        return TracedAcquireAsync(policy, policyName, key, cost, cancellationToken);
    }

    public ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken) {
        IRateLimitAlgorithm? defaultAlgorithm = this._defaultPolicy.Value;
        if(defaultAlgorithm is null) {
            throw new InvalidOperationException("No default rate limiting policy is configured. Use UseDefaultPolicy(...) during setup.");
        }

        return TracedAcquireAsync(defaultAlgorithm, RateLimitingTracing.DefaultPolicyName, key, cost, cancellationToken);
    }

    private static async ValueTask<RateLimitDecision> TracedAcquireAsync(
        IRateLimitAlgorithm algorithm,
        string policyName,
        string key,
        int cost,
        CancellationToken cancellationToken) {

        using Activity? activity = RateLimitingTracing.StartAcquire(policyName, key, cost);

        RateLimitDecision decision = await algorithm.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);
        RateLimitingTracing.RecordDecision(activity, decision);

        return decision;
    }

    public IRateLimitAlgorithm GetPolicy(string policyName) {
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        return this._resolvedPolicies.GetOrAdd(policyName, name => {
            if(this._options.Policies.TryGetValue(name, out Func<IServiceProvider, IRateLimitAlgorithm>? factory)) {
                return factory(this._serviceProvider);
            }

            throw new KeyNotFoundException($"Rate limiting policy '{name}' was not found. Ensure it is registered via AddPolicy(\"{name}\", ...).");
        });
    }
}