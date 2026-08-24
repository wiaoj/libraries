using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A weighted-window <see cref="IRateLimitAlgorithm"/> — the technique Cloudflare describes for
/// approximating a true sliding window cheaply: blend the previous fixed window's count (weighted
/// by how much of "now" still overlaps it) with the current fixed window's count.
/// </summary>
/// <remarks>
/// <para>
/// Two adjacent <see cref="IDistributedCounter"/> instances back each key — one per fixed window
/// id (<c>{key}:{windowId}</c> and <c>{key}:{windowId - 1}</c>), where <c>windowId</c> is the
/// window duration divided into absolute UTC time. Windows are aligned to absolute time, not to
/// a per-key "first request" timestamp — this is what lets two independently-computed window ids
/// (this request's "current" and the next request's "previous") always refer to the same physical
/// window.
/// </para>
/// <para>
/// Unlike <see cref="FixedWindowRateLimiter"/>, there's no single atomic "increment across two
/// counters, but only if under the weighted limit" primitive. So this algorithm:
/// <list type="number">
/// <item><description>Reads the previous window's count (cheap, uncontended — nobody else is writing to a window that's already closed).</description></item>
/// <item><description>Speculatively, atomically increments the *current* window's counter by <c>cost</c>.</description></item>
/// <item><description>Computes the weighted estimate; if it exceeds the limit, rolls back the increment with a matching decrement and denies.</description></item>
/// </list>
/// This is a known trade-off of the technique, not a bug: under heavy concurrent load right at the
/// boundary, a handful of requests may be briefly counted and rolled back rather than never counted
/// at all. It also means this is an *approximation* — not a mathematically exact sliding window
/// (that's <c>SlidingWindowLogRateLimiter</c>, backed by a sorted set, for when exactness matters
/// more than memory/cost).
/// </para>
/// </remarks>
public sealed class SlidingWindowRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "SlidingWindowWeighted";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SlidingWindowRateLimiter> _logger;

    /// <summary>
    /// Creates a new weighted sliding-window rate limiter.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given window key.</param>
    /// <param name="limit">The maximum total cost allowed per key within any rolling window-length lookback. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window)
        : this(counterFactory, limit, window, TimeProvider.System, NullLogger<SlidingWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new weighted sliding-window rate limiter with a custom time provider.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given window key.</param>
    /// <param name="limit">The maximum total cost allowed per key within any rolling window-length lookback. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving window boundaries. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(counterFactory, limit, window, timeProvider, NullLogger<SlidingWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new weighted sliding-window rate limiter with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given window key.</param>
    /// <param name="limit">The maximum total cost allowed per key within any rolling window-length lookback. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving window boundaries. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<SlidingWindowRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);

        this._counterFactory = counterFactory;
        this._limit = limit;
        this._window = window;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        long windowTicks = this._window.Ticks;
        long nowTicks = this._timeProvider.GetUtcNow().UtcTicks;

        long currentWindowId = nowTicks / windowTicks;
        long elapsedTicks = nowTicks - (currentWindowId * windowTicks);
        double previousWeight = 1.0 - ((double)elapsedTicks / windowTicks);

        IDistributedCounter currentCounter = this._counterFactory.Create($"{key}:{currentWindowId}");
        IDistributedCounter previousCounter = this._counterFactory.Create($"{key}:{currentWindowId - 1}");

        // Keep a window's counter alive for 2x its own duration: it's only ever read as "previous"
        // during the window immediately following it, so this is exactly enough headroom without
        // leaking stale windows forever.
        CounterExpiry expiry = CounterExpiry.From(this._window * 2);

        long previousCount = (await previousCounter.GetValueAsync(cancellationToken).ConfigureAwait(false)).Value;

        CounterValue currentAfterIncrement = await currentCounter
            .IncrementAsync(cost, expiry, cancellationToken)
            .ConfigureAwait(false);

        double estimatedTotal = (previousCount * previousWeight) + currentAfterIncrement.Value;

        if(estimatedTotal > this._limit) {
            // Roll back the speculative increment — a denied attempt must never permanently
            // consume capacity another (smaller, or later) request could have used.
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