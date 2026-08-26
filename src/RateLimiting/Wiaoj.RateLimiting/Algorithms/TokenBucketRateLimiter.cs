using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
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

        MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();
        RateLimitDecision fallbackDenied = RateLimitDecision.Denied(this._window, remaining: 0);

        RateLimitDecision decision = this._state.CompareAndSwap(
            key,
            (cost, capacity: this._capacity, refillPerSec: this._refillPerSecond, now),
            static (current, state) => {
                if(current is null) {
                    BucketState initial = new(state.capacity - state.cost, state.now);
                    return (initial, RateLimitDecision.Allowed((long)initial.Tokens), true);
                }
                 
                BucketState existing = current.Value;
                double elapsed = Math.Max(0, (state.now - existing.LastRefill).TotalSeconds);
                double refilled = Math.Min(state.capacity, existing.Tokens + (elapsed * state.refillPerSec));
                 
                if(refilled < state.cost) {
                    double deficit = state.cost - refilled;
                    TimeSpan retryAfter = TimeSpan.FromSeconds(deficit / state.refillPerSec);
                    return (default, RateLimitDecision.Denied(retryAfter, remaining: (long)refilled), false);
                }
                 
                BucketState next = new(refilled - state.cost, state.now);
                return (next, RateLimitDecision.Allowed((long)next.Tokens), true);
            },
            fallbackDenied,
            cancellationToken);

        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, decision);
        return ValueTask.FromResult(decision);

        //while(!cancellationToken.IsCancellationRequested) {
        //    if(!this._state.TryGetValue(key, out BucketState current)) {
        //        BucketState initialState = new(this._capacity - cost, now);
        //        if(this._state.TryAdd(key, initialState)) {
        //            RateLimitDecision firstAllowed = RateLimitDecision.Allowed((long)initialState.Tokens);
        //            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, firstAllowed);
        //            return ValueTask.FromResult(firstAllowed);
        //        }

        //        continue;
        //    }

        //    double elapsedSeconds = Math.Max(0, (now - current.LastRefill).TotalSeconds);
        //    double refilled = Math.Min(this._capacity, current.Tokens + (elapsedSeconds * this._refillPerSecond));

        //    if(refilled < cost) {
        //        double deficit = cost - refilled;
        //        TimeSpan retryAfter = TimeSpan.FromSeconds(deficit / this._refillPerSecond);
        //        RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: (long)refilled);
        //        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
        //        return ValueTask.FromResult(deniedDecision);
        //    }

        //    BucketState nextState = new(refilled - cost, now);

        //    if(this._state.TryUpdate(key, nextState, current)) {
        //        RateLimitDecision allowedDecision = RateLimitDecision.Allowed((long)nextState.Tokens);
        //        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        //        return ValueTask.FromResult(allowedDecision);
        //    }
        //}

        //return ValueTask.FromResult(RateLimitDecision.Denied(this._window, remaining: 0));
    }

    /// <summary>
    /// Clears all tracked in-memory bucket state.
    /// </summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct BucketState(double Tokens, MonotonicTimestamp LastRefill);
}