using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.RateLimiting.Diagnostics;
using Wiaoj.RateLimiting.Internal;

namespace Wiaoj.RateLimiting;

/// <summary>
/// A leaky-bucket-as-queue traffic shaping <see cref="IRateLimitAlgorithm"/> that smooths bursts by delaying admitted requests.
/// Utilizes <see cref="MonotonicTimestamp"/> to guarantee immune operation against system wall-clock skew and NTP corrections.
/// </summary>
public sealed class LeakyBucketQueueRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "LeakyBucketQueue";
    private readonly int _capacity;
    private readonly TimeSpan _period;
    private readonly long _emissionIntervalTicks;
    private readonly long _maxBacklogTicks;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<LeakyBucketQueueRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, MonotonicTimestamp> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakyBucketQueueRateLimiter"/> class.
    /// </summary>
    /// <param name="capacity">The maximum queue backlog capacity before requests are rejected.</param>
    /// <param name="period">The duration required for a full backlog to drain to empty.</param>
    public LeakyBucketQueueRateLimiter(
        int capacity,
        TimeSpan period)
        : this(capacity, period, TimeProvider.System, NullLogger<LeakyBucketQueueRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakyBucketQueueRateLimiter"/> class with a custom time provider.
    /// </summary>
    /// <param name="capacity">The maximum queue backlog capacity before requests are rejected.</param>
    /// <param name="period">The duration required for a full backlog to drain to empty.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    public LeakyBucketQueueRateLimiter(
        int capacity,
        TimeSpan period,
        TimeProvider timeProvider)
        : this(capacity, period, timeProvider, NullLogger<LeakyBucketQueueRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="LeakyBucketQueueRateLimiter"/> class with a custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="capacity">The maximum queue backlog capacity before requests are rejected.</param>
    /// <param name="period">The duration required for a full backlog to drain to empty.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="logger">The logger instance.</param>
    public LeakyBucketQueueRateLimiter(
        int capacity,
        TimeSpan period,
        TimeProvider timeProvider,
        ILogger<LeakyBucketQueueRateLimiter> logger) {
        Preca.ThrowIfNegativeOrZero(capacity);
        Preca.ThrowIfNegativeOrZero(period);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._capacity = capacity;
        this._period = period;
        this._emissionIntervalTicks = period.Ticks / capacity;
        this._maxBacklogTicks = this._emissionIntervalTicks * capacity;
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
            RateLimitDecision overCapacityDecision = RateLimitDecision.Denied(this._period, remaining: this._capacity);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, overCapacityDecision);
            return ValueTask.FromResult(overCapacityDecision);
        }

        MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();
        TimeSpan incrementDuration = TimeSpan.FromTicks(this._emissionIntervalTicks * cost);

        while(!cancellationToken.IsCancellationRequested) {
            bool exists = this._state.TryGetValue(key, out MonotonicTimestamp existingTat);
            MonotonicTimestamp baseline = (!exists || now > existingTat) ? now : existingTat;
            MonotonicTimestamp newTat = baseline + incrementDuration;

            TimeSpan backlog = newTat - now;

            if(backlog.Ticks > this._maxBacklogTicks) {
                MonotonicTimestamp rejectAllowAt = newTat - TimeSpan.FromTicks(this._maxBacklogTicks);
                TimeSpan retryAfter = TimeSpan.Max((rejectAllowAt - now), TimeSpan.Zero);
                RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
                RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
                return ValueTask.FromResult(deniedDecision);
            }

            bool updated = exists
                ? this._state.TryUpdate(key, newTat, existingTat)
                : this._state.TryAdd(key, newTat);

            if(updated) {
                TimeSpan wait = TimeSpan.Max((baseline - now), TimeSpan.Zero);

                if(wait == TimeSpan.Zero) {
                    long remaining = GcraMath.ComputeRemaining(newTat, now, this._maxBacklogTicks, this._emissionIntervalTicks);
                    RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
                    RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
                    return ValueTask.FromResult(allowedDecision);
                }

                RateLimitingDiagnostics.RecordQueueSuspended(this._logger, AlgorithmName, key, cost, wait);
                return new ValueTask<RateLimitDecision>(WaitForTurnAndCompleteAsync(key, cost, incrementDuration, wait, cancellationToken));
            }
        }

        return ValueTask.FromResult(RateLimitDecision.Denied(this._period, remaining: 0));
    }

    private async Task<RateLimitDecision> WaitForTurnAndCompleteAsync(
        string key,
        int cost,
        TimeSpan incrementDuration,
        TimeSpan wait,
        CancellationToken cancellationToken) {

        long startTimestamp = this._timeProvider.GetTimestamp();
        try {
            await this._timeProvider.Delay(wait, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) {
            RollbackTat(key, incrementDuration);
            RateLimitingDiagnostics.RecordQueueCancelled(this._logger, AlgorithmName, key, cost);
            throw;
        }

        TimeSpan actualElapsed = this._timeProvider.GetElapsedTime(startTimestamp);
        RateLimitingDiagnostics.RecordQueueReleased(this._logger, AlgorithmName, key, actualElapsed);

        MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();
        MonotonicTimestamp currentTat = this._state.GetValueOrDefault(key, now);
        long remaining = GcraMath.ComputeRemaining(currentTat, now, this._maxBacklogTicks, this._emissionIntervalTicks);
        RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);

        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return allowedDecision;
    }

    private void RollbackTat(string key, TimeSpan rollbackDuration) {
        while(this._state.TryGetValue(key, out MonotonicTimestamp existing)) {
            MonotonicTimestamp rolledBack = existing - rollbackDuration;
            if(this._state.TryUpdate(key, rolledBack, existing)) {
                break;
            }
        }
    }

    /// <summary>
    /// Clears all tracked in-memory queue state.
    /// </summary>
    public void Reset() {
        this._state.Clear();
    }
}