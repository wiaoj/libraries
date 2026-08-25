using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A resilient decorator for <see cref="IRateLimitAlgorithm"/> that provides Fail-Open semantics.
/// If the underlying distributed store fails or throws an exception, requests are allowed through.
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
    /// <param name="logger">The logger instance.</param>
    public ResilientRateLimiter(
        IRateLimitAlgorithm inner,
        ILogger<ResilientRateLimiter> logger) {
        Preca.ThrowIfNull(inner);
        Preca.ThrowIfNull(logger);

        this._inner = inner;
        this._algorithmName = inner.GetType().Name;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        try {
            return await this._inner.TryAcquireAsync(key, cost, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception ex) {
            this._logger.LogStorageFailureFallback(key, this._algorithmName, ex);
            return RateLimitDecision.Allowed();
        }
    }
}