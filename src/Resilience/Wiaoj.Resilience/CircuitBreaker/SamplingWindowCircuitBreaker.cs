using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Diagnostics;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic percentage-based circuit breaker strategy calculating error rates over rolling time windows.
/// Uses bounded concurrent probe gating during half-open recovery.
/// </summary>
public sealed class SamplingWindowCircuitBreaker : ICircuitBreaker {
    private const string StrategyName = "SamplingWindow";
    private const string PolicyCategory = "CircuitBreaker";

    private readonly IDistributedCounterFactory _counterFactory;
    private readonly SamplingWindowCircuitBreakerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SamplingWindowCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingWindowCircuitBreaker"/> class.
    /// </summary>
    public SamplingWindowCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        SamplingWindowCircuitBreakerOptions options)
        : this(counterFactory, options, TimeProvider.System, NullLogger<SamplingWindowCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingWindowCircuitBreaker"/> class with a custom time provider.
    /// </summary>
    public SamplingWindowCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        SamplingWindowCircuitBreakerOptions options,
        TimeProvider timeProvider)
        : this(counterFactory, options, timeProvider, NullLogger<SamplingWindowCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingWindowCircuitBreaker"/> class with custom time provider and logger.
    /// </summary>
    public SamplingWindowCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        SamplingWindowCircuitBreakerOptions options,
        TimeProvider timeProvider,
        ILogger<SamplingWindowCircuitBreaker> logger) {
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
        IDistributedCounter trippedCounter = this._counterFactory.Create(PolicyCategory, trippedKey);

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

            // Half-Open: Allow up to N permitted concurrent trial probes
            string probeKey = FormatProbeKey(key);
            IDistributedCounter probeCounter = this._counterFactory.Create(PolicyCategory, probeKey);

            CounterLimitResult probeClaim = await probeCounter.TryIncrementAsync(
                amount: 1,
                limit: this._options.PermittedNumberOfCallsInHalfOpenState,
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

        long windowId = GetCurrentWindowId();
        string successKey = FormatSuccessKey(key, windowId);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter successCounter = this._counterFactory.Create(PolicyCategory, successKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create(PolicyCategory, trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(PolicyCategory, probeKey);

        CounterExpiry expiry = CounterExpiry.From(this._options.SamplingWindow * 2);
        await successCounter.IncrementAsync(1, expiry, cancellationToken).ConfigureAwait(false);

        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        bool wasTripped = trippedVal.Value > 0;

        if(trippedVal.Value > 0) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset blockedUntil = new(trippedVal.Value, TimeSpan.Zero);

            if(now >= blockedUntil) {
                await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
                await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
                ResilienceDiagnostics.RecordSuccess(this._logger, StrategyName, key, wasRecovered: true);
                return;
            }
        }

        ResilienceDiagnostics.RecordSuccess(this._logger, StrategyName, key, wasRecovered: wasTripped);
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);
        IDistributedCounter trippedCounter = this._counterFactory.Create(PolicyCategory, trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(PolicyCategory, probeKey);

        CounterValue currentTrip = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(currentTrip.Value > 0) {
            await TripAsync(trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
            return;
        }

        long windowId = GetCurrentWindowId();
        string successKey = FormatSuccessKey(key, windowId);
        string failureKey = FormatFailureKey(key, windowId);

        IDistributedCounter successCounter = this._counterFactory.Create(PolicyCategory, successKey);
        IDistributedCounter failureCounter = this._counterFactory.Create(PolicyCategory, failureKey);

        CounterExpiry expiry = CounterExpiry.From(this._options.SamplingWindow * 2);
        CounterValue newFailureVal = await failureCounter.IncrementAsync(1, expiry, cancellationToken).ConfigureAwait(false);
        CounterValue successVal = await successCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        long totalRequests = successVal.Value + newFailureVal.Value;
        double failureRate = totalRequests > 0 ? (double)newFailureVal.Value / totalRequests : 0.0;

        ResilienceDiagnostics.RecordFailure(this._logger, StrategyName, key, failureRate);

        if(totalRequests >= this._options.MinimumThroughput && failureRate >= this._options.FailureRateThreshold) {
            await TripAsync(trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
            ResilienceDiagnostics.RecordTrip(this._logger, StrategyName, key, "SamplingWindowFailureRateThresholdExceeded", this._options.BreakDuration);
        }
    }

    private async ValueTask TripAsync(IDistributedCounter trippedCounter, IDistributedCounter probeCounter, CancellationToken cancellationToken) {
        DateTimeOffset blockedUntil = this._timeProvider.GetUtcNow().Add(this._options.BreakDuration);
        CounterExpiry tripExpiry = CounterExpiry.From(this._options.BreakDuration * 2);

        await trippedCounter.SetAsync(blockedUntil.UtcTicks, tripExpiry, cancellationToken).ConfigureAwait(false);
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    private long GetCurrentWindowId() {
        return this._timeProvider.GetUtcNow().UtcTicks / this._options.SamplingWindow.Ticks;
    }

    private string FormatSuccessKey(string key, long windowId) {
        return $"{this._options.KeyPrefix}sw:succ:{key}:{windowId}";
    }

    private string FormatFailureKey(string key, long windowId) {
        return $"{this._options.KeyPrefix}sw:fail:{key}:{windowId}";
    }

    private string FormatTrippedKey(string key) {
        return $"{this._options.KeyPrefix}sw:open:{key}";
    }

    private string FormatProbeKey(string key) {
        return $"{this._options.KeyPrefix}sw:probe:{key}";
    }
}