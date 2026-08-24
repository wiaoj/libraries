using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A leaky-bucket-as-meter <see cref="IRateLimitAlgorithm"/>: each key owns a bucket that fills
/// by <c>cost</c> with every accepted request and continuously <b>leaks</b> back down to zero at a
/// constant rate (<c>capacity / period</c> units per second). A request is allowed only if adding
/// its cost would not overflow the bucket past <c>capacity</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the mirror image of <see cref="TokenBucketRateLimiter"/>, not a different
/// decision procedure.</b> Where token bucket tracks tokens <i>available</i> and drains them as
/// requests spend them, this tracks a level of accumulated <i>usage</i> and lets it drain away on
/// its own. For the same <c>capacity</c>/<c>period</c>, both accept and deny exactly the same
/// sequence of requests — <c>level == capacity - tokens</c> at every point in time. It is also the
/// classic algorithm <see cref="GcraRateLimiter"/> implements more compactly as a single
/// projected-time value (GCRA is textbook-equivalent to "leaky bucket as meter" — see the type
/// remarks there). This class exists anyway because "leaky bucket" is what many people reach for
/// by name, and because expressing configuration as a drain/leak rate rather than a refill rate or
/// a time projection is sometimes the more natural fit for a given call site.
/// </para>
/// <para>
/// <b>Not the queueing/shaping variant.</b> Some systems use "leaky bucket" to mean something
/// meaningfully different: incoming requests are queued and released at a smoothed, constant
/// output rate rather than being rejected outright (e.g. nginx's <c>limit_req burst=N;</c> without
/// <c>nodelay</c>). That variant would make <see cref="TryAcquireAsync"/> actually await until a
/// request's turn arrives — a real behavioral difference from every other algorithm in this
/// package, all of which decide synchronously. It's deliberately out of scope here to keep this
/// type's decisions immediate and directly comparable/testable against its siblings; a
/// <c>LeakyBucketQueueRateLimiter</c> variant is available for queue-based shaping.
/// </para>
/// <para>
/// <b>State shape:</b> like <see cref="TokenBucketRateLimiter"/> (and unlike the single-scalar
/// <see cref="GcraRateLimiter"/>), a meter-form leaky bucket needs two values updated atomically
/// together — the current level and the timestamp of the last leak calculation. This implementation
/// stores state in a <see cref="ConcurrentDictionary{TKey,TValue}"/> updated via <c>AddOrUpdate</c>'s
/// CAS loop — atomic and correct for a single process, but <b>not distributed</b>, for the same
/// reasons documented on <see cref="TokenBucketRateLimiter"/>.
/// </para>
/// <para>
/// <b>Leak on denial:</b> a denied request still advances the bucket's timestamp and applies
/// whatever partial leak accrued since the last check — only the requested <c>cost</c> is withheld
/// from being added. This matches standard leaky-bucket semantics (the bucket keeps draining
/// regardless of whether any particular request is accepted) and is what makes
/// <see cref="RateLimitDecision.RetryAfter"/> meaningful: it's computed from how much still needs
/// to leak away before this request's cost would fit, not a fixed window.
/// </para>
/// </remarks>
public sealed class LeakyBucketRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "LeakyBucketMeter";
    private readonly int _capacity;
    private readonly double _leakPerSecond;
    private readonly TimeSpan _period;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LeakyBucketRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, BucketState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Creates a new leaky-bucket (meter) rate limiter.
    /// </summary>
    /// <param name="capacity">The maximum level the bucket can hold before it overflows. Must be greater than zero.</param>
    /// <param name="period">The time it takes a full bucket to leak back down to empty. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public LeakyBucketRateLimiter(
        int capacity,
        TimeSpan period)
        : this(capacity, period, TimeProvider.System, NullLogger<LeakyBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new leaky-bucket (meter) rate limiter with a custom time provider.
    /// </summary>
    /// <param name="capacity">The maximum level the bucket can hold before it overflows. Must be greater than zero.</param>
    /// <param name="period">The time it takes a full bucket to leak back down to empty. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving leak calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public LeakyBucketRateLimiter(
        int capacity,
        TimeSpan period,
        TimeProvider timeProvider)
        : this(capacity, period, timeProvider, NullLogger<LeakyBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new leaky-bucket (meter) rate limiter with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="capacity">The maximum level the bucket can hold before it overflows. Must be greater than zero.</param>
    /// <param name="period">The time it takes a full bucket to leak back down to empty. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="timeProvider">The time provider driving leak calculations. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public LeakyBucketRateLimiter(
        int capacity,
        TimeSpan period,
        TimeProvider timeProvider,
        ILogger<LeakyBucketRateLimiter> logger) {
        Preca.ThrowIfNegativeOrZero(capacity);
        Preca.ThrowIfNegativeOrZero(period);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._capacity = capacity;
        this._period = period;
        this._leakPerSecond = capacity / period.TotalSeconds;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._capacity) {
            // No amount of leaking ever lets this succeed — an empty bucket still can't absorb it.
            RateLimitDecision overCapacityDecision = RateLimitDecision.Denied(this._period, remaining: this._capacity);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, overCapacityDecision);
            return ValueTask.FromResult(overCapacityDecision);
        }

        DateTimeOffset now = this._timeProvider.GetUtcNow();
        bool allowed = false;
        double levelAfter = 0;

        this._state.AddOrUpdate(
            key,
            addValueFactory: _ => {
                // First-ever request for this key: bucket starts empty.
                allowed = true;
                levelAfter = cost;
                return new BucketState(levelAfter, now);
            },
            updateValueFactory: (_, existing) => {
                double elapsedSeconds = Math.Max(0, (now - existing.LastLeak).TotalSeconds);
                double leaked = elapsedSeconds * this._leakPerSecond;
                double currentLevel = Math.Max(0, existing.Level - leaked);

                if(currentLevel + cost <= this._capacity) {
                    allowed = true;
                    levelAfter = currentLevel + cost;
                    return new BucketState(levelAfter, now);
                }

                // Denied — but the leak that accrued since the last check is real and must be
                // kept (along with the advanced timestamp), or a burst of denied requests would
                // itself stall the bucket's drain progress. Only the requested cost is withheld.
                allowed = false;
                levelAfter = currentLevel;
                return new BucketState(currentLevel, now);
            });

        if(!allowed) {
            double overflow = (levelAfter + cost) - this._capacity;
            TimeSpan retryAfter = TimeSpan.FromSeconds(overflow / this._leakPerSecond);
            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: (long)Math.Max(0, this._capacity - levelAfter));
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return ValueTask.FromResult(deniedDecision);
        }

        RateLimitDecision allowedDecision = RateLimitDecision.Allowed((long)Math.Max(0, this._capacity - levelAfter));
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return ValueTask.FromResult(allowedDecision);
    }

    /// <summary>Clears all tracked bucket state. Useful between test cases if the instance is reused.</summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct BucketState(double Level, DateTimeOffset LastLeak);
}