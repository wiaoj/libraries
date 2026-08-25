using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A distributed weighted sliding-window <see cref="IRateLimitAlgorithm"/> approximating a rolling lookback window.
/// </summary>
public sealed class SlidingWindowRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "SlidingWindowWeighted";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly string _policyName;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowRateLimiter"/> class with default policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window)
        : this(counterFactory, "SlidingWindow", limit, window, TimeProvider.System, NullLogger<SlidingWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowRateLimiter"/> class with a specific policy name.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan window)
        : this(counterFactory, policyName, limit, window, TimeProvider.System, NullLogger<SlidingWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowRateLimiter"/> class with custom time provider.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(counterFactory, policyName, limit, window, timeProvider, NullLogger<SlidingWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowRateLimiter"/> class with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">The counter factory used to resolve backing storage counters.</param>
    /// <param name="policyName">The name of the rate limiting policy.</param>
    /// <param name="limit">The maximum cost allowed per window.</param>
    /// <param name="window">The window duration.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="logger">The logger instance.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        string policyName,
        int limit,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<SlidingWindowRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._counterFactory = counterFactory;
        this._policyName = policyName;
        this._limit = limit;
        this._window = window;
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

        long windowTicks = this._window.Ticks;
        long nowTicks = this._timeProvider.GetUtcNow().UtcTicks;

        long currentWindowId = nowTicks / windowTicks;
        long elapsedTicks = nowTicks - (currentWindowId * windowTicks);
        double previousWeight = 1.0 - ((double)elapsedTicks / windowTicks);

        IDistributedCounter currentCounter = this._counterFactory.Create(this._policyName, $"{key}:{currentWindowId}");
        IDistributedCounter previousCounter = this._counterFactory.Create(this._policyName, $"{key}:{currentWindowId - 1}");

        CounterExpiry expiry = CounterExpiry.From(this._window * 2);

        long previousCount = (await previousCounter.GetValueAsync(cancellationToken).ConfigureAwait(false)).Value;

        CounterValue currentAfterIncrement = await currentCounter
            .IncrementAsync(cost, expiry, cancellationToken)
            .ConfigureAwait(false);

        double estimatedTotal = (previousCount * previousWeight) + currentAfterIncrement.Value;

        if(estimatedTotal > this._limit) {
            await currentCounter.DecrementAsync(cost, expiry, cancellationToken).ConfigureAwait(false);
            RateLimitingDiagnostics.RecordRollback(this._logger, AlgorithmName, key, cost, "EstimatedTotalExceededLimit");

            TimeSpan retryAfter = TimeSpan.FromTicks(windowTicks - elapsedTicks);
            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return deniedDecision;
        }

        long remaining = (long)Math.Max(0, this._limit - estimatedTotal);
        RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return allowedDecision;
    }
}