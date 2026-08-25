using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// An in-memory token-bucket <see cref="IRateLimitAlgorithm"/> that allows bursts up to capacity and refills at a steady rate.
/// </summary>
public sealed class TokenBucketRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "TokenBucket";
    private readonly int _capacity;
    private readonly double _refillPerSecond;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenBucketRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, BucketState> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketRateLimiter"/> class.
    /// </summary>
    /// <param name="capacity">The maximum token capacity.</param>
    /// <param name="window">The duration required to refill from empty to full capacity.</param>
    public TokenBucketRateLimiter(
        int capacity,
        TimeSpan window)
        : this(capacity, window, TimeProvider.System, NullLogger<TokenBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketRateLimiter"/> class with a custom time provider.
    /// </summary>
    /// <param name="capacity">The maximum token capacity.</param>
    /// <param name="window">The duration required to refill from empty to full capacity.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    public TokenBucketRateLimiter(
        int capacity,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(capacity, window, timeProvider, NullLogger<TokenBucketRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenBucketRateLimiter"/> class with a custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="capacity">The maximum token capacity.</param>
    /// <param name="window">The duration required to refill from empty to full capacity.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="logger">The logger instance.</param>
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

    /// <inheritdoc/>
    public ValueTask<RateLimitDecision> TryAcquireAsync(
        string key,
        int cost,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrEmpty(key);
        Preca.ThrowIfNegativeOrZero(cost);

        cancellationToken.ThrowIfCancellationRequested();

        if(cost > this._capacity) {
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

    /// <summary>
    /// Clears all tracked in-memory bucket state.
    /// </summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct BucketState(double Tokens, DateTimeOffset LastRefill);
}