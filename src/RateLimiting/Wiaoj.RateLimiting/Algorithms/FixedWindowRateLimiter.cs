using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A distributed fixed-window <see cref="IRateLimitAlgorithm"/> backed by <see cref="IDistributedCounterFactory"/>.
/// </summary>
public sealed class FixedWindowRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "FixedWindow";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly string _policyName;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ILogger<FixedWindowRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedWindowRateLimiter"/> class with default policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    public FixedWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window)
        : this(counterFactory, "FixedWindow", limit, window, NullLogger<FixedWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedWindowRateLimiter"/> class with a specific policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    public FixedWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan window)
        : this(counterFactory, policyName, limit, window, NullLogger<FixedWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedWindowRateLimiter"/> class with policy name and diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    /// <param name="logger">The logger instance.</param>
    public FixedWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan window,
        ILogger<FixedWindowRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(logger);

        this._counterFactory = counterFactory;
        this._policyName = policyName;
        this._limit = limit;
        this._window = window;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);
         
        IDistributedCounter counter = this._counterFactory.Create(this._policyName, key);

        CounterLimitResult result = await counter
            .TryIncrementAsync(cost, this._limit, CounterExpiry.From(this._window), cancellationToken)
            .ConfigureAwait(false);

        if(result.IsAllowed) {
            RateLimitDecision allowedDecision = RateLimitDecision.Allowed(result.Remaining);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
            return allowedDecision;
        }

        TimeSpan retryAfter = result.Ttl is { } ttl && ttl > TimeSpan.Zero ? ttl : this._window;
        RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, result.Remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);

        return deniedDecision;
    }
}