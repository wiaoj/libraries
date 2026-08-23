using Wiaoj.DistributedCounter;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A fixed-window <see cref="IRateLimitAlgorithm"/> built directly on top of
/// <see cref="IDistributedCounterFactory"/>'s limit-aware <c>TryIncrementAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// This type deliberately does very little on its own: the atomic "increment, but reject
/// past a ceiling" semantics already live in <see cref="IDistributedCounter.TryIncrementAsync"/>
/// (and, for the Redis storage, in a single Lua round-trip). This class exists to translate
/// that counter-shaped result (<see cref="CounterLimitResult"/>) into a rate-limiting-shaped
/// one (<see cref="RateLimitDecision"/>).
/// </para>
/// <para>
/// <see cref="RateLimitDecision.RetryAfter"/> is read straight from <see cref="CounterLimitResult.Ttl"/>
/// — the same round-trip that performed the increment already knows the window's remaining TTL
/// (Redis via <c>PTTL</c> inside the Lua script, in-memory via the CAS'd expiry). There is no
/// second storage call: querying TTL separately would mean paying an extra network hop precisely
/// when the system is already under the most load (denied requests cluster during abuse/bursts).
/// </para>
/// <para>
/// Known burst behavior at window boundaries (e.g. a client can send up to <c>2 × limit</c>
/// requests across a window seam) is inherent to fixed windows, not a bug in this
/// implementation. Use <see cref="SlidingWindowRateLimiter"/> when that matters.
/// </para>
/// </remarks>
public sealed class FixedWindowRateLimiter : IRateLimitAlgorithm {
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly int _limit;
    private readonly TimeSpan _window;

    /// <summary>
    /// Creates a new fixed-window rate limiter.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given key.</param>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public FixedWindowRateLimiter(IDistributedCounterFactory counterFactory, int limit, TimeSpan window) {
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
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if(cost <= 0) {
            throw new ArgumentOutOfRangeException(nameof(cost), "Cost must be greater than zero.");
        }

        IDistributedCounter counter = this._counterFactory.Create(key);

        CounterLimitResult result = await counter
            .TryIncrementAsync(cost, this._limit, CounterExpiry.From(this._window), cancellationToken)
            .ConfigureAwait(false);

        if(result.IsAllowed) {
            return RateLimitDecision.Allowed(result.Remaining);
        }

        // Denied — the counter result already carries the window's actual remaining TTL from the
        // same round-trip. Fall back to the full window only if storage genuinely couldn't
        // determine it (e.g. the window hadn't been established yet) — a conservative
        // over-estimate is safer than telling a caller "retry now" when they can't yet.
        TimeSpan retryAfter = result.Ttl is { } ttl && ttl > TimeSpan.Zero ? ttl : this._window;

        return RateLimitDecision.Denied(retryAfter, result.Remaining);
    }
}