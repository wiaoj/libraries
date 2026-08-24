using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// An exact sliding-window <see cref="IRateLimitAlgorithm"/>: every accepted request is recorded
/// as an individual log entry (timestamp + cost); a request is allowed only if the sum of costs
/// still inside the trailing <c>window</c> lookback — recomputed fresh on every call — does not
/// exceed <c>limit</c>. Unlike <see cref="SlidingWindowRateLimiter"/> (which blends two adjacent
/// fixed windows as a cheap approximation), this is the "real" sliding window the README calls out
/// as the exactness/cost trade-off: no boundary burst is possible, but memory and per-request work
/// scale with the number of requests actually seen inside a window, not with a constant.
/// </summary>
/// <remarks>
/// <para>
/// <b>How entries expire:</b> each entry is evicted independently, exactly <c>window</c> after it
/// was recorded — not all at once the way a fixed window resets. A key that has been steadily
/// making requests never sees a sudden full reset; capacity trickles back in as old entries age out.
/// This is the property <see cref="SlidingWindowRateLimiter"/> only approximates via weighting.
/// </para>
/// <para>
/// <b>State shape:</b> this needs an ordered, atomically-trimmable log per key — conceptually a
/// Redis sorted set scored by timestamp (<c>ZADD</c> to record, <c>ZREMRANGEBYSCORE</c> to evict,
/// <c>ZCARD</c>/summed scores to count). A plain <see cref="DistributedCounter.IDistributedCounter"/>
/// (a single <c>long</c> + TTL) can't express this, the same primitive mismatch
/// <see cref="TokenBucketRateLimiter"/> calls out for its own state. This implementation therefore
/// stores each key's log as an in-process <see cref="List{T}"/> guarded by a per-key lock — correct
/// and atomic for a single process, but <b>not distributed</b>. A distributed backend would swap
/// this for a Redis sorted set (or equivalent) behind the same trim-then-count-then-conditionally-add
/// shape; this class is the reference behavior such an implementation should match in tests.
/// </para>
/// <para>
/// <b>Rollback on denial:</b> a request is speculatively evaluated — expired entries are trimmed
/// and the candidate total is computed — before deciding whether to append. A denied request is
/// never appended, so it never occupies capacity another request could have used, and the trim
/// itself (removing genuinely expired entries) is preserved either way since it isn't a rollback
/// candidate, it's just bookkeeping that's always correct to apply.
/// </para>
/// </remarks>
public sealed class SlidingWindowLogRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "SlidingWindowLog";
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SlidingWindowLogRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, KeyLog> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new exact sliding-window-log rate limiter.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within any rolling lookback. Must be greater than zero.</param>
    /// <param name="window">The lookback duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window)
        : this(limit, window, TimeProvider.System, NullLogger<SlidingWindowLogRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new exact sliding-window-log rate limiter with a custom time provider.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within any rolling lookback. Must be greater than zero.</param>
    /// <param name="window">The lookback duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving timestamps. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(limit, window, timeProvider, NullLogger<SlidingWindowLogRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new exact sliding-window-log rate limiter with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed per key within any rolling lookback. Must be greater than zero.</param>
    /// <param name="window">The lookback duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving timestamps. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<SlidingWindowLogRateLimiter> logger) {
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._limit = limit;
        this._window = window;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset windowStart = now - this._window;

        KeyLog log = this._state.GetOrAdd(key, static _ => new KeyLog());
        (bool allowed, long totalCost, DateTimeOffset? oldestExisting) = log.TryAdd(now, windowStart, cost, this._limit);

        if(!allowed) {
            // No prior (still-live) entry to measure against means this is effectively a
            // first-ever-for-this-window denial (e.g. cost alone exceeds the limit) — fall back to
            // the full window rather than claiming "0s until you can retry".
            TimeSpan retryAfter = oldestExisting is { } oldest ? (oldest + this._window) - now : this._window;
            if(retryAfter < TimeSpan.Zero) {
                retryAfter = TimeSpan.Zero;
            }

            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return ValueTask.FromResult(deniedDecision);
        }

        long remaining = Math.Max(0, this._limit - totalCost);
        RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return ValueTask.FromResult(allowedDecision);
    }

    /// <summary>Clears all tracked state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct LogEntry(DateTimeOffset Timestamp, int Cost);

    /// <summary>
    /// Per-key append/trim/count log. A plain lock (rather than a lock-free CAS loop like
    /// <see cref="TokenBucketRateLimiter"/> uses) is the right tool here: list mutation — removing
    /// an arbitrary number of expired entries and conditionally appending — isn't naturally
    /// expressible as a single compare-and-swap over an immutable value without either rebuilding
    /// the whole list on every attempt or accepting more complexity than a short critical section
    /// buys back for what is, per key, low-contention work.
    /// </summary>
    private sealed class KeyLog {
        private readonly List<LogEntry> _entries = [];
        private readonly object _gate = new();

        public (bool Allowed, long TotalCost, DateTimeOffset? OldestExisting) TryAdd(
            DateTimeOffset now, DateTimeOffset windowStart, int cost, int limit) {
            lock(this._gate) {
                this._entries.RemoveAll(entry => entry.Timestamp < windowStart);

                DateTimeOffset? oldestExisting = null;
                long existingCost = 0;
                foreach(LogEntry entry in this._entries) {
                    existingCost += entry.Cost;
                    if(oldestExisting is null || entry.Timestamp < oldestExisting) {
                        oldestExisting = entry.Timestamp;
                    }
                }

                long total = existingCost + cost;
                if(total > limit) {
                    return (false, total, oldestExisting);
                }

                this._entries.Add(new LogEntry(now, cost));
                return (true, total, oldestExisting);
            }
        }
    }
}