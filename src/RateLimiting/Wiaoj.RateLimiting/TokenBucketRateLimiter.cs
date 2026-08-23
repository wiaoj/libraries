using System.Collections.Concurrent;

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
    private readonly int _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, BucketState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new token-bucket rate limiter.
    /// </summary>
    /// <param name="capacity">
    /// The maximum number of tokens the bucket can hold — i.e. the largest burst a fully-idle key
    /// can absorb instantly. Must be greater than zero.
    /// </param>
    /// <param name="window">
    /// The time it takes to refill an empty bucket to full capacity at the steady rate. The
    /// effective refill rate is <c>capacity / window</c> tokens/second. Must be greater than
    /// <see cref="TimeSpan.Zero"/>.
    /// </param>
    /// <param name="timeProvider">
    /// The time provider driving refill calculations. Pass a
    /// <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> in tests; defaults to
    /// <see cref="TimeProvider.System"/> when omitted.
    /// </param>
    public TokenBucketRateLimiter(int capacity, TimeSpan window, TimeProvider? timeProvider = null) {
        if(capacity <= 0) {
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
        }
        if(window <= TimeSpan.Zero) {
            throw new ArgumentOutOfRangeException(nameof(window), "Window must be greater than zero.");
        }

        this._capacity = capacity;
        this._window = window;
        this._refillPerSecond = capacity / window.TotalSeconds;
        this._timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._capacity) {
            // No amount of refilling ever lets this succeed — a full bucket still can't cover it.
            return ValueTask.FromResult(RateLimitDecision.Denied(this._window, remaining: this._capacity));
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
            return ValueTask.FromResult(RateLimitDecision.Denied(retryAfter, remaining: (long)tokensAfter));
        }

        return ValueTask.FromResult(RateLimitDecision.Allowed((long)tokensAfter));
    }

    /// <summary>Clears all tracked bucket state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct BucketState(double Tokens, DateTimeOffset LastRefill);
}