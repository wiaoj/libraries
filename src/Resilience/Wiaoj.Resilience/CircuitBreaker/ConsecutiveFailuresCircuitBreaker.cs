using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Diagnostics;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic circuit breaker strategy that trips to <see cref="CircuitState.Open"/>
/// when consecutive transient failures reach a configured threshold.
/// </summary>
public sealed class ConsecutiveFailuresCircuitBreaker : ICircuitBreaker {
    private const string StrategyName = "ConsecutiveFailures";

    private readonly IDistributedCounterFactory _counterFactory;
    private readonly CircuitBreakerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConsecutiveFailuresCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsecutiveFailuresCircuitBreaker"/> class.
    /// </summary>
    public ConsecutiveFailuresCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        CircuitBreakerOptions options)
        : this(counterFactory, options, TimeProvider.System, NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsecutiveFailuresCircuitBreaker"/> class with a custom time provider.
    /// </summary>
    public ConsecutiveFailuresCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        CircuitBreakerOptions options,
        TimeProvider timeProvider)
        : this(counterFactory, options, timeProvider, NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsecutiveFailuresCircuitBreaker"/> class with custom time provider and logger.
    /// </summary>
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
        IDistributedCounter trippedCounter = this._counterFactory.Create<CircuitBreakerTag, string>(trippedKey);

        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if(trippedVal.Value > 0) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset blockedUntil = new(trippedVal.Value, TimeSpan.Zero);

            if(blockedUntil > now) {
                TimeSpan retryAfter = blockedUntil - now;
                CircuitExecutionDecision deniedDecision = CircuitExecutionDecision.Denied(retryAfter);
                ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, deniedDecision);
                return deniedDecision;
            }

            string probeKey = FormatProbeKey(key);
            IDistributedCounter probeCounter = this._counterFactory.Create<CircuitBreakerTag, string>(probeKey);

            CounterLimitResult probeClaim = await probeCounter.TryIncrementAsync(
                amount: 1,
                limit: 1,
                expiry: CounterExpiry.From(this._options.BreakDuration * 2),
                cancellationToken).ConfigureAwait(false);

            if(probeClaim.IsAllowed) {
                CircuitExecutionDecision probeDecision = CircuitExecutionDecision.HalfOpenProbe();
                ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, probeDecision);
                return probeDecision;
            }

            CircuitExecutionDecision deniedProbeDecision = CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(1));
            ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, deniedProbeDecision);
            return deniedProbeDecision;
        }

        CircuitExecutionDecision allowedDecision = CircuitExecutionDecision.Allowed();
        ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, allowedDecision);
        return allowedDecision;
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string failuresKey = FormatFailuresKey(key);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter failuresCounter = this._counterFactory.Create<CircuitBreakerTag, string>(failuresKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create<CircuitBreakerTag, string>(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create<CircuitBreakerTag, string>(probeKey);

        await failuresCounter.ResetAsync(cancellationToken).ConfigureAwait(false);

        CounterValue currentTripVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if(currentTripVal.Value > 0) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset blockedUntil = new(currentTripVal.Value, TimeSpan.Zero);

            if(now >= blockedUntil) {
                await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
                await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
                ResilienceDiagnostics.RecordSuccess(this._logger, StrategyName, key, wasRecovered: true);
                return;
            }
        }

        ResilienceDiagnostics.RecordSuccess(this._logger, StrategyName, key, wasRecovered: false);
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string failuresKey = FormatFailuresKey(key);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter failuresCounter = this._counterFactory.Create<CircuitBreakerTag, string>(failuresKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create<CircuitBreakerTag, string>(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create<CircuitBreakerTag, string>(probeKey);

        CounterValue currentTripVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(currentTripVal.Value > 0) {
            await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
            return;
        }

        CounterExpiry failureExpiry = CounterExpiry.From(this._options.BreakDuration * 2);
        CounterValue newFailureCount = await failuresCounter.IncrementAsync(1, failureExpiry, cancellationToken).ConfigureAwait(false);

        ResilienceDiagnostics.RecordFailure(this._logger, StrategyName, key, newFailureCount.Value);

        if(newFailureCount.Value >= this._options.FailureThreshold) {
            await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask TripAsync(string key, IDistributedCounter trippedCounter, IDistributedCounter probeCounter, CancellationToken cancellationToken) {
        DateTimeOffset blockedUntil = this._timeProvider.GetUtcNow().Add(this._options.BreakDuration);
        CounterExpiry tripExpiry = CounterExpiry.From(this._options.BreakDuration * 2);

        await trippedCounter.SetAsync(blockedUntil.UtcTicks, tripExpiry, cancellationToken).ConfigureAwait(false);
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);

        ResilienceDiagnostics.RecordTrip(this._logger, StrategyName, key, "ConsecutiveFailuresThresholdExceeded", this._options.BreakDuration);
    }

    private string FormatFailuresKey(string key) {
        return $"{this._options.KeyPrefix}cf:fail:{key}";
    }

    private string FormatTrippedKey(string key) {
        return $"{this._options.KeyPrefix}cf:open:{key}";
    }

    private string FormatProbeKey(string key) {
        return $"{this._options.KeyPrefix}cf:probe:{key}";
    }
}