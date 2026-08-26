using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.RateLimiting.Diagnostics;

namespace Wiaoj.RateLimiting;

/// <summary>
/// An exact sliding-window log <see cref="IRateLimitAlgorithm"/> that tracks individual request timestamps.
/// </summary>
public sealed class SlidingWindowLogRateLimiter : IRateLimitAlgorithm {
    private const string AlgorithmName = "SlidingWindowLog";
    private readonly int _limit;
    private readonly TimeSpan _window;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SlidingWindowLogRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, KeyLog> _state = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowLogRateLimiter"/> class.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed within any rolling lookback window.</param>
    /// <param name="window">The rolling lookback window duration.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window)
        : this(limit, window, TimeProvider.System, NullLogger<SlidingWindowLogRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowLogRateLimiter"/> class with a custom time provider.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed within any rolling lookback window.</param>
    /// <param name="window">The rolling lookback window duration.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window,
        TimeProvider timeProvider)
        : this(limit, window, timeProvider, NullLogger<SlidingWindowLogRateLimiter>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowLogRateLimiter"/> class with a custom time provider and diagnostic logging.
    /// </summary>
    /// <param name="limit">The maximum total cost allowed within any rolling lookback window.</param>
    /// <param name="window">The rolling lookback window duration.</param>
    /// <param name="timeProvider">The time provider instance.</param>
    /// <param name="logger">The logger instance.</param>
    public SlidingWindowLogRateLimiter(
        int limit,
        TimeSpan window,
        TimeProvider timeProvider,
        ILogger<SlidingWindowLogRateLimiter> logger) {
        Preca.ThrowIfNegativeOrZero(limit);
        Preca.ThrowIfNegativeOrZero(window);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._limit = limit;
        this._window = window;
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

        MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();
        MonotonicTimestamp windowStart = now - this._window;

        KeyLog log = this._state.GetOrAdd(key, static _ => new KeyLog());
        (bool allowed, long totalCost, MonotonicTimestamp? oldestExisting) = log.TryAdd(now, windowStart, cost, this._limit);

        if(!allowed) {
            TimeSpan retryAfter = oldestExisting is { } oldest ? (oldest + this._window) - now : this._window;
            if(retryAfter < TimeSpan.Zero) {
                retryAfter = TimeSpan.Zero;
            }

            RateLimitDecision deniedDecision = RateLimitDecision.Denied(retryAfter, remaining: 0);
            RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, deniedDecision);
            return ValueTask.FromResult(deniedDecision);
        }

        long remaining = Math.Max(0, this._limit - totalCost);
        RateLimitDecision allowedDecision = RateLimitDecision.Allowed(remaining);
        RateLimitingDiagnostics.RecordDecision(this._logger, AlgorithmName, key, cost, allowedDecision);
        return ValueTask.FromResult(allowedDecision);
    }

    /// <summary>
    /// Clears all tracked in-memory timestamp log state.
    /// </summary>
    public void Reset() {
        this._state.Clear();
    }

    private readonly record struct LogEntry(MonotonicTimestamp Timestamp, int Cost);

    private sealed class KeyLog {
        private readonly List<LogEntry> _entries = [];
        private readonly object _gate = new();

        public (bool Allowed, long TotalCost, MonotonicTimestamp? OldestExisting) TryAdd(
            MonotonicTimestamp now, MonotonicTimestamp windowStart, int cost, int limit) {
            lock(this._gate) {
                this._entries.RemoveAll(entry => entry.Timestamp < windowStart);

                MonotonicTimestamp? oldestExisting = null;
                long existingCost = 0;
                foreach(LogEntry entry in this._entries) {
                    existingCost += entry.Cost;
                    if(oldestExisting is null || entry.Timestamp < oldestExisting) {
                        oldestExisting = entry.Timestamp;
                    }
                }

                long total = existingCost + cost;
                if(total > limit) {
                    return (false, total, oldestExisting);
                }

                this._entries.Add(new LogEntry(now, cost));
                return (true, total, oldestExisting);
            }
        }
    }
}