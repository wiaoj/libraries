using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting.Resilience;

/// <summary>
/// A resilient decorator for <see cref="IRateLimitAlgorithm"/> that provides Fail-Open semantics.
/// If the underlying distributed store (e.g. Redis) fails or throws a network/storage exception,
/// this decorator logs the error and gracefully allows the request through rather than failing the API call.
/// </summary>
public sealed class ResilientRateLimiter : IRateLimitAlgorithm {
    private readonly IRateLimitAlgorithm _inner;
    private readonly ILogger<ResilientRateLimiter> _logger;
    private readonly string _algorithmName;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientRateLimiter"/> class.
    /// </summary>
    /// <param name="inner">The underlying rate limiting algorithm to guard.</param>
    public ResilientRateLimiter(IRateLimitAlgorithm inner)
        : this(inner, NullLogger<ResilientRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilientRateLimiter"/> class with diagnostic logging.
    /// </summary>
    /// <param name="inner">The underlying rate limiting algorithm to guard.</param>
    /// <param name="logger">Optional logger for logging storage failure fallbacks.</param>
    public ResilientRateLimiter(
        IRateLimitAlgorithm inner,
        ILogger<ResilientRateLimiter> logger) {
        Preca.ThrowIfNull(inner);
        Preca.ThrowIfNull(logger);

        this._inner = inner;
        this._algorithmName = inner.GetType().Name;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);
        try {
            return await this._inner.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            // Caller cancellation must never be swallowed
            throw;
        }
        catch(Exception ex) {
            // Storage or network error: Log and execute Fail-Open fallback (Allow request)
            this._logger.LogStorageFailureFallback(key, this._algorithmName, ex);
            return RateLimitDecision.Allowed(remaining: null);
        }
    }
}