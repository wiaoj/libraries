using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

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
    private const string AlgorithmName = "FixedWindow";
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly ILogger<FixedWindowRateLimiter> _logger;

    /// <summary>
    /// Creates a new fixed-window rate limiter.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given key.</param>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    public FixedWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window)
        : this(counterFactory, limit, window, NullLogger<FixedWindowRateLimiter>.Instance) { }

    /// <summary>
    /// Creates a new fixed-window rate limiter with diagnostic logging.
    /// </summary>
    /// <param name="counterFactory">Resolves the <see cref="IDistributedCounter"/> backing a given key.</param>
    /// <param name="limit">The maximum total cost allowed per key within a single window. Must be greater than zero.</param>
    /// <param name="window">The window duration. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <param name="logger">Optional logger for structured diagnostic logging.</param>
    public FixedWindowRateLimiter(
        IDistributedCounterFactory counterFactory,
        int limit,
        TimeSpan window,
        ILogger<FixedWindowRateLimiter> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);

        this._counterFactory = counterFactory;
        this._limit = limit;
        this._window = window;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        IDistributedCounter counter = this._counterFactory.Create(key);

        CounterLimitResult result = await counter
            .TryIncrementAsync(cost, this._limit, CounterExpiry.From(this._window), cancellationToken)
            .ConfigureAwait(false);

        if(result.IsAllowed) {
            RateLimitDecision allowedDecision = RateLimitDecision.Allowed(result.Remaining);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
            return allowedDecision;
        }

        // Denied — the counter result already carries the window's actual remaining TTL from the
        // same round-trip. Fall back to the full window only if storage genuinely couldn't
        // determine it (e.g. the window hadn't been established yet) — a conservative
        // over-estimate is safer than telling a caller "retry now" when they can't yet.
        TimeSpan retryAfter = result.Ttl is { } ttl && ttl > TimeSpan.Zero ? ttl : this._window;
        RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, result.Remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);

        return deniedDecision;
    }
}