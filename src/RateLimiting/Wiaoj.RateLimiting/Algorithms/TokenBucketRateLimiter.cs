using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A token-bucket <see cref="IRateLimitAlgorithm"/>: each key owns a bucket of <c>capacity</c>
/// tokens that refill continuously at a constant rate (<c>capacity / window</c> tokens per second).
/// Unlike <see cref="FixedWindowRateLimiter"/> and <see cref="SlidingWindowRateLimiter"/>, this
/// algorithm explicitly tolerates bursts: a bucket that has been idle can absorb up to
/// <c>capacity</c> requests instantly, then throttles back down to the steady refill rate. This is
/// the trade-off the other two algorithms don't offer — useful when occasional spikes are fine as
/// long as sustained throughput stays bounded.
/// </summary>
/// <remarks>
/// <para>
/// <b>State shape:</b> a token bucket needs two values updated atomically together — the current
/// token count and the timestamp of the last refill. A plain <see cref="DistributedCounter.IDistributedCounter"/>
/// (a single <c>long</c> + TTL) can't express this, which is exactly the primitive mismatch the
/// README calls out for this algorithm. This implementation stores state in a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> and updates it via <c>AddOrUpdate</c>'s CAS
/// loop — atomic and correct for a single process, but <b>not distributed</b>: multiple instances
/// each get their own bucket. A distributed backend (e.g. Redis) would need a Lua script that reads
/// both fields, computes the refill, and writes both back in one round-trip — the same shape as
/// <c>DistributedCounterRedisLuaScripts.IncrementIfLessThan</c>, just with two stored fields instead
/// of one. That's future work for a Redis-backed sibling of this class; this one is the correct
/// choice for single-instance deployments or as the reference behavior distributed implementations
/// should match in tests.
/// </para>
/// <para>
/// <b>Refill on denial:</b> a denied request still advances the bucket's timestamp and applies
/// whatever partial refill accrued since the last check — only the requested <c>cost</c> tokens are
/// withheld. This matches standard token-bucket semantics (tokens accrue independently of whether
/// any particular request succeeds) and is what makes <see cref="RateLimitDecision.RetryAfter"/>
/// meaningful: it's computed from the actual token deficit at the moment of denial, not a fixed window.
/// </para>
/// </remarks>
public sealed class TokenBucketRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "TokenBucket";
    private readonly int _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, BucketState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new token-bucket rate limiter.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens the bucket can hold. Must be greater than zero.</param>
    /// <param name="window">The time it takes to refill an empty bucket to full capacity. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public TokenBucketRateLimiter(
        int capacity,
        TimeSpan window)
        : this(capacity, window, TimeProvider.System, NullLogger<TokenBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new token-bucket rate limiter with a custom time provider.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens the bucket can hold. Must be greater than zero.</param>
    /// <param name="window">The time it takes to refill an empty bucket to full capacity. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving refill calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public TokenBucketRateLimiter(
        int capacity,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(capacity, window, timeProvider, NullLogger<TokenBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new token-bucket rate limiter with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="capacity">The maximum number of tokens the bucket can hold. Must be greater than zero.</param>
    /// <param name="window">The time it takes to refill an empty bucket to full capacity. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving refill calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public TokenBucketRateLimiter(
        int capacity,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<TokenBucketRateLimiter> logger) {
        Preca.ThrowIfNegativeOrZero(capacity);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._capacity = capacity;
        this._window = window;
        this._refillPerSecond = capacity / window.TotalSeconds;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key); 
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._capacity) {
            // No amount of refilling ever lets this succeed — a full bucket still can't cover it.
            RateLimitDecision overCapacityDecision = RateLimitDecision.Denied(this._window, remaining: this._capacity);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, overCapacityDecision);
            return ValueTask.FromResult(overCapacityDecision);
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        bool allowed = false;
        double tokensAfter = 0;

        this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                // First-ever request for this key: bucket starts full.
                allowed = true;
                tokensAfter = this._capacity - cost;
                return new BucketState(tokensAfter, now);
            },
            updateValueFactory: (_, existing) => {
                double elapsedSeconds = Math.Max(0, (now - existing.LastRefill).TotalSeconds);
                double refilled = Math.Min(this._capacity, existing.Tokens + (elapsedSeconds * this._refillPerSecond));

                if(refilled >= cost) {
                    allowed = true;
                    tokensAfter = refilled - cost;
                    return new BucketState(tokensAfter, now);
                }

                // Denied — but the refill that accrued since the last check is real and must be
                // kept (along with the advanced timestamp), or a burst of denied requests would
                // itself stall the bucket's refill progress. Only the requested cost is withheld.
                allowed = false;
                tokensAfter = refilled;
                return new BucketState(refilled, now);
            });

        if(!allowed) {
            double deficit = cost - tokensAfter;
            TimeSpan retryAfter = TimeSpan.FromSeconds(deficit / this._refillPerSecond);
            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: (long)tokensAfter);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return ValueTask.FromResult(deniedDecision);
        }

        RateLimitDecision allowedDecision = RateLimitDecision.Allowed((long)tokensAfter);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return ValueTask.FromResult(allowedDecision);
    }

    /// <summary>Clears all tracked bucket state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct BucketState(double Tokens, DateTimeOffset LastRefill);
}