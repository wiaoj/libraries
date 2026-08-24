using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;
using Wiaoj.RateLimiting.Internal;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A distributed <see cref="IRateLimitAlgorithm"/> implementing the Generic Cell Rate Algorithm (GCRA)
/// on top of <see cref="IDistributedCounterFactory"/>.
/// </summary>
/// <remarks>
/// GCRA tracks a single scalar Theoretical Arrival Time (TAT) in UTC ticks. Because it represents
/// state as a single scalar timestamp, it can be distributed across multi-node clusters via
/// <see cref="IDistributedCounter"/> without requiring complex multi-key Lua scripts.
/// </remarks>
public sealed class DistributedGcraRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "DistributedGcra";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly int _limit;
    private readonly TimeSpan _period;
    private readonly long _emissionIntervalTicks;
    private readonly long _burstToleranceTicks;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DistributedGcraRateLimiter> _logger;

    /// <summary>
    /// Creates a new distributed GCRA rate limiter.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing the TAT key.</param>
    /// <param name="limit">The maximum burst size a fully-idle key can absorb instantly. Must be greater than zero.</param>
    /// <param name="period">The time it takes a drained key to recover its full burst allowance. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public DistributedGcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan period)
        : this(counterFactory, limit, period, TimeProvider.System, NullLogger<DistributedGcraRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new distributed GCRA rate limiter with a custom time provider.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing the TAT key.</param>
    /// <param name="limit">The maximum burst size a fully-idle key can absorb instantly. Must be greater than zero.</param>
    /// <param name="period">The time it takes a drained key to recover its full burst allowance. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving TAT calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public DistributedGcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan period,
        TimeProvider timeProvider)
        : this(counterFactory, limit, period, timeProvider, NullLogger<DistributedGcraRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new distributed GCRA rate limiter with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing the TAT key.</param>
    /// <param name="limit">The maximum burst size a fully-idle key can absorb instantly. Must be greater than zero.</param>
    /// <param name="period">The time it takes a drained key to recover its full burst allowance. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving TAT calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public DistributedGcraRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan period,
        TimeProvider timeProvider,
        ILogger<DistributedGcraRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(period);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._counterFactory = counterFactory;
        this._limit = limit;
        this._period = period;
        this._emissionIntervalTicks = period.Ticks / limit;
        this._burstToleranceTicks = this._emissionIntervalTicks * limit;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key); 
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._limit) {
            RateLimitDecision overLimitDecision = RateLimitDecision.Denied(this._period, remaining: this._limit);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, overLimitDecision);
            return overLimitDecision;
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        long nowTicks = now.UtcTicks;
        long incrementTicks = this._emissionIntervalTicks * cost;

        IDistributedCounter counter = this._counterFactory.Create(key);
        CounterExpiry expiry = CounterExpiry.From(this._period * 2);

        // Read stored TAT from distributed storage (0 if key does not exist yet)
        CounterValue currentCounterVal = await counter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        long existingTatTicks = currentCounterVal.Value;

        long baseline = Math.Max(existingTatTicks, nowTicks);
        long newTatTicks = baseline + incrementTicks;
        long allowAtTicks = newTatTicks - this._burstToleranceTicks;

        if(nowTicks < allowAtTicks) {
            // Denied: Stored TAT is not updated
            TimeSpan retryAfter = TimeSpan.FromTicks(Math.Max(0, allowAtTicks - nowTicks));
            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return deniedDecision;
        }

        // Allowed: Advance TAT in storage
        long deltaToApply = newTatTicks - existingTatTicks;
        await counter.IncrementAsync(deltaToApply, expiry, cancellationToken).ConfigureAwait(false);

        DateTimeOffset newTat = new(newTatTicks, TimeSpan.Zero);
        long remaining = GcraMath.ComputeRemaining(newTat, now, this._burstToleranceTicks, this._emissionIntervalTicks);
        RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);

        return allowedDecision;
    }
}