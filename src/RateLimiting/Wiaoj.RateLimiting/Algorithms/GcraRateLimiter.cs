using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;
using Wiaoj.RateLimiting.Internal;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A Generic Cell Rate Algorithm (GCRA) <see cref="IRateLimitAlgorithm"/> implemented on top of
/// <see cref="IDistributedCounterFactory"/> using optimistic concurrency (Compare-And-Swap).
/// </summary>
public sealed class GcraRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "Gcra";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly string _policyName;
    private readonly int _limit;
    private readonly TimeSpan _period;
    private readonly long _emissionIntervalTicks;
    private readonly long _burstToleranceTicks;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GcraRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GcraRateLimiter"/> class with default policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="limit">The burst capacity limit.</param>
    /// <param name="period">The period required to drain full burst capacity.</param>
    public GcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan period)
        : this(counterFactory, "Gcra", limit, period, TimeProvider.System, NullLogger<GcraRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GcraRateLimiter"/> class with a specific policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The burst capacity limit.</param>
    /// <param name="period">The period required to drain full burst capacity.</param>
    public GcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan period)
        : this(counterFactory, policyName, limit, period, TimeProvider.System, NullLogger<GcraRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GcraRateLimiter"/> class with custom time provider.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The burst capacity limit.</param>
    /// <param name="period">The period required to drain full burst capacity.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    public GcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan period,
        TimeProvider timeProvider)
        : this(counterFactory, policyName, limit, period, timeProvider, NullLogger<GcraRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="GcraRateLimiter"/> class with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The burst capacity limit.</param>
    /// <param name="period">The period required to drain full burst capacity.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="logger">The logger instance.</param>
    public GcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan period,
        TimeProvider timeProvider,
        ILogger<GcraRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(period);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._counterFactory = counterFactory;
        this._policyName = policyName;
        this._limit = limit;
        this._period = period;
        this._emissionIntervalTicks = period.Ticks / limit;
        this._burstToleranceTicks = this._emissionIntervalTicks * limit;
        this._timeProvider = timeProvider;
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

        if(cost > this._limit) {
            RateLimitDecision overLimitDecision = RateLimitDecision.Denied(this._period, remaining: this._limit);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, overLimitDecision);
            return overLimitDecision;
        }

        long incrementTicks = this._emissionIntervalTicks * cost;
        IDistributedCounter counter = this._counterFactory.Create(this._policyName, key);
        CounterExpiry expiry = CounterExpiry.From(this._period * 2);

        while(!cancellationToken.IsCancellationRequested) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            long nowTicks = now.UtcTicks;

            CounterValue currentCounterVal = await counter.GetValueAsync(cancellationToken).ConfigureAwait(false);
            long existingTatTicks = currentCounterVal.Value;

            long baseline = Math.Max(existingTatTicks, nowTicks);
            long newTatTicks = baseline + incrementTicks;
            long allowAtTicks = newTatTicks - this._burstToleranceTicks;

            if(nowTicks < allowAtTicks) {
                TimeSpan retryAfter = TimeSpan.FromTicks(Math.Max(0, allowAtTicks - nowTicks));
                RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
                RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
                return deniedDecision;
            }

            bool success = await counter.TryCompareExchangeAsync(
                expectedValue: currentCounterVal,
                newValue: new CounterValue(newTatTicks),
                expiry: expiry,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if(success) {
                DateTimeOffset newTat = new(newTatTicks, TimeSpan.Zero);
                long remaining = GcraMath.ComputeRemaining(newTat, now, this._burstToleranceTicks, this._emissionIntervalTicks);
                RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
                RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
                return allowedDecision;
            }
        }

        return RateLimitDecision.Denied(this._period, remaining: 0);
    }
}