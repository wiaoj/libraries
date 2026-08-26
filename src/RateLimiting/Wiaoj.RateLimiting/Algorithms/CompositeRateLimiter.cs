using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A composite <see cref="IRateLimitAlgorithm"/> that evaluates a sequence of rate limiting tiers.
/// All tiers must allow the acquisition for the request to proceed.
/// </summary>
public sealed class CompositeRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "Composite";
    private readonly IReadOnlyList<IRateLimitAlgorithm> _algorithms;
    private readonly ILogger<CompositeRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeRateLimiter"/> class.
    /// </summary>
    /// <param name="algorithms">The sequence of rate limiting algorithms to evaluate in order.</param>
    public CompositeRateLimiter(params IReadOnlyList<IRateLimitAlgorithm> algorithms) : this(algorithms, NullLogger<CompositeRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeRateLimiter"/> class with diagnostic logging.
    /// </summary>
    /// <param name="algorithms">The sequence of rate limiting algorithms to evaluate in order.</param>
    /// <param name="logger">The logger instance.</param>
    public CompositeRateLimiter(IReadOnlyList<IRateLimitAlgorithm> algorithms, ILogger<CompositeRateLimiter> logger) {
        Preca.ThrowIfNull(algorithms);
        Preca.ThrowIfNull(logger);

        if(algorithms.Count == 0) {
            throw new ArgumentException("Composite rate limiter requires at least one algorithm tier.", nameof(algorithms));
        }

        this._algorithms = algorithms;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        long minRemaining = long.MaxValue;
        TimeSpan maxRetryAfter = TimeSpan.Zero;

        for(int i = 0; i < this._algorithms.Count; i++) {
            IRateLimitAlgorithm algorithm = this._algorithms[i];
            RateLimitDecision decision = await algorithm.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);

            if(!decision.IsAllowed) {
                TimeSpan retryAfter = decision.RetryAfter ?? TimeSpan.Zero;
                if(retryAfter > maxRetryAfter) {
                    maxRetryAfter = retryAfter;
                }

                RateLimitDecision deniedDecision = RateLimitDecision.Denied(maxRetryAfter, decision.Remaining ?? 0);
                RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
                return deniedDecision;
            }

            if(decision.Remaining.HasValue && decision.Remaining.Value < minRemaining) {
                minRemaining = decision.Remaining.Value;
            }
        }

        long? effectiveRemaining = minRemaining == long.MaxValue ? null : minRemaining;
        RateLimitDecision allowedDecision = effectiveRemaining.HasValue
            ? RateLimitDecision.Allowed(effectiveRemaining.Value)
            : RateLimitDecision.Allowed();

        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return allowedDecision;
    }
}