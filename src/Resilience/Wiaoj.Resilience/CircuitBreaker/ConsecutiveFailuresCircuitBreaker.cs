using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic circuit breaker strategy that trips to <see cref="CircuitState.Open"/>
/// when consecutive transient failures reach a configured threshold.
/// </summary>
public sealed class ConsecutiveFailuresCircuitBreaker : ICircuitBreaker {
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly CircuitBreakerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsecutiveFailuresCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsecutiveFailuresCircuitBreaker"/> class.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="options">The circuit breaker options.</param>
    public ConsecutiveFailuresCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        CircuitBreakerOptions options)
        : this(counterFactory, options, TimeProvider.System, NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsecutiveFailuresCircuitBreaker"/> class with custom time provider and logger.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="options">The circuit breaker options.</param>
    /// <param name="timeProvider">The time provider driving timestamps and TTL calculations.</param>
    /// <param name="logger">The logger instance.</param>
    public ConsecutiveFailuresCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        CircuitBreakerOptions options,
        TimeProvider timeProvider,
        ILogger<ConsecutiveFailuresCircuitBreaker> logger) {
        Preca.ThrowIfNull(counterFactory);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        options.Validate();
        this._counterFactory = counterFactory;
        this._options = options;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        string trippedKey = FormatTrippedKey(key);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);

        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if(trippedVal.Value > 0) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset blockedUntil = new(trippedVal.Value, TimeSpan.Zero);

            if(blockedUntil > now) {
                TimeSpan retryAfter = blockedUntil - now;
                return CircuitExecutionDecision.Denied(retryAfter);
            }

            // Half-Open state: Attempt atomic single-probe claim (limit 1)
            string probeKey = FormatProbeKey(key);
            IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

            CounterLimitResult probeClaim = await probeCounter.TryIncrementAsync(
                amount: 1,
                limit: 1,
                expiry: CounterExpiry.From(this._options.BreakDuration * 2),
                cancellationToken).ConfigureAwait(false);

            if(probeClaim.IsAllowed) {
                return CircuitExecutionDecision.HalfOpenProbe();
            }

            // Another concurrent request already claimed the single trial probe: fast-fail
            return CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(1));
        }

        return CircuitExecutionDecision.Allowed();
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string failuresKey = FormatFailuresKey(key);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter failuresCounter = this._counterFactory.Create(failuresKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

        await failuresCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
        await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string failuresKey = FormatFailuresKey(key);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter failuresCounter = this._counterFactory.Create(failuresKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

        // Immediate re-trip if failure occurs during an open or half-open state
        CounterValue currentTripVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(currentTripVal.Value > 0) {
            await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
            return;
        }

        CounterExpiry failureExpiry = CounterExpiry.From(this._options.BreakDuration * 2);
        CounterValue newFailureCount = await failuresCounter.IncrementAsync(1, failureExpiry, cancellationToken).ConfigureAwait(false);

        if(newFailureCount.Value >= this._options.FailureThreshold) {
            await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);

            this._logger.LogWarning("[ConsecutiveFailures] Circuit breaker TRIPPED to OPEN for key '{Key}'. Consecutive failures: {Failures}. Break duration: {DurationMs:F0}ms.",
                key, newFailureCount.Value, this._options.BreakDuration.TotalMilliseconds);
        }
    }

    private async ValueTask TripAsync(string key, IDistributedCounter trippedCounter, IDistributedCounter probeCounter, CancellationToken cancellationToken) {
        DateTimeOffset blockedUntil = this._timeProvider.GetUtcNow().Add(this._options.BreakDuration);
        CounterExpiry tripExpiry = CounterExpiry.From(this._options.BreakDuration * 2);

        await trippedCounter.SetAsync(blockedUntil.UtcTicks, tripExpiry, cancellationToken).ConfigureAwait(false);
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string FormatFailuresKey(string key) => $"wh:cb:cf:fail:{key}";
    private static string FormatTrippedKey(string key) => $"wh:cb:cf:open:{key}";
    private static string FormatProbeKey(string key) => $"wh:cb:cf:probe:{key}";
}