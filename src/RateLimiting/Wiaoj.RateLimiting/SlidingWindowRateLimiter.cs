using Wiaoj.DistributedCounter;

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
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Creates a new weighted sliding-window rate limiter.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given window key.</param>
    /// <param name="limit">The maximum total cost allowed per key within any rolling window-length lookback. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">
    /// The time provider driving window boundaries. Pass a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in tests;
    /// defaults to <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public SlidingWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window,
        TimeProvider? timeProvider = null) {
        ArgumentNullException.ThrowIfNull(counterFactory);
        if(limit <= 0) {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }
        if(window <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be greater than zero.");
        }

        this._counterFactory = counterFactory;
        this._limit = limit;
        this._window = window;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

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

            TimeSpan retryAfter = TimeSpan.FromTicks(windowTicks - elapsedTicks);
            return RateLimitDecision.Denied(retryAfter, remaining: 0);
        }

        long remaining = (long)Math.Max(0, this._limit - estimatedTotal);
        return RateLimitDecision.Allowed(remaining);
    }
}