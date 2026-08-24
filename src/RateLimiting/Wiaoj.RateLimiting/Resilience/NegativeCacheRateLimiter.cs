using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting.Resilience;

/// <summary>
/// A high-performance L1 negative-cache decorator that short-circuits rate-limited keys directly in memory,
/// protecting the underlying distributed store (e.g. Redis) from high-frequency spam and DDoS attacks.
/// </summary>
public sealed class NegativeCacheRateLimiter : IRateLimitAlgorithm {
    private readonly IRateLimitAlgorithm _inner;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<NegativeCacheRateLimiter> _logger;
    private readonly string _algorithmName;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _denialCache = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="NegativeCacheRateLimiter"/> class.
    /// </summary>
    /// <param name="inner">The underlying rate limiting algorithm to guard.</param>
    public NegativeCacheRateLimiter(IRateLimitAlgorithm inner)
        : this(inner, TimeProvider.System, NullLogger<NegativeCacheRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NegativeCacheRateLimiter"/> class with a custom time provider.
    /// </summary>
    /// <param name="inner">The underlying rate limiting algorithm to guard.</param>
    /// <param name="timeProvider">The time provider driving cache expiration. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    public NegativeCacheRateLimiter(
        IRateLimitAlgorithm inner,
        TimeProvider timeProvider)
        : this(inner, timeProvider, NullLogger<NegativeCacheRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NegativeCacheRateLimiter"/> class with custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="inner">The underlying rate limiting algorithm to guard.</param>
    /// <param name="timeProvider">The time provider driving cache expiration. Defaults to <see cref="TimeProvider.System"/> when omitted.</param>
    /// <param name="logger">Optional logger for logging short-circuit events.</param>
    public NegativeCacheRateLimiter(
        IRateLimitAlgorithm inner,
        TimeProvider timeProvider,
        ILogger<NegativeCacheRateLimiter> logger) {
        Preca.ThrowIfNull(inner);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._inner = inner;
        this._algorithmName = inner.GetType().Name;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        DateTimeOffset now = this._timeProvider.GetUtcNow();
         
        if(this._denialCache.TryGetValue(key, out DateTimeOffset blockedUntil)) {
            if(now < blockedUntil) { 
                TimeSpan retryAfter = blockedUntil - now;
                if(this._logger.IsEnabled(LogLevel.Debug)) {
                    this._logger.LogNegativeCacheHit(key, this._algorithmName, retryAfter.TotalSeconds);
                }
                return RateLimitDecision.Denied(retryAfter, remaining: 0);
            }
             
            this._denialCache.TryRemove(key, out _);
        }
         
        RateLimitDecision decision = await this._inner.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);
         
        if(!decision.IsAllowed && decision.RetryAfter is { } retryAfterSpan && retryAfterSpan > TimeSpan.Zero) {
            DateTimeOffset blockTarget = now.Add(retryAfterSpan);
            this._denialCache[key] = blockTarget;
        }

        return decision;
    }

    /// <summary>
    /// Clears all tracked in-memory denial cache state.
    /// </summary>
    public void Reset() {
        this._denialCache.Clear();
    }
}