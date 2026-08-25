using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Preconditions;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic percentage-based circuit breaker strategy calculating error rates over rolling time windows.
/// Uses bounded concurrent probe gating during half-open recovery.
/// </summary>
public sealed class SamplingWindowCircuitBreaker : ICircuitBreaker {
    private readonly IDistributedCounterFactory _counterFactory;
    private readonly SamplingWindowCircuitBreakerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SamplingWindowCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingWindowCircuitBreaker"/> class.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="options">The sampling window options.</param>
    public SamplingWindowCircuitBreaker(
        IDistributedCounterFactory counterFactory,
        SamplingWindowCircuitBreakerOptions options)
        : this(counterFactory, options, TimeProvider.System, NullLogger<SamplingWindowCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingWindowCircuitBreaker"/> class with custom time provider and logger.
    /// </summary>
    /// <param name="counterFactory">The distributed counter factory.</param>
    /// <param name="options">The sampling window options.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="logger">The logger instance.</param>
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
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);

        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        if(trippedVal.Value > 0) {
            DateTimeOffset now = this._timeProvider.GetUtcNow();
            DateTimeOffset blockedUntil = new(trippedVal.Value, TimeSpan.Zero);

            if(blockedUntil > now) {
                TimeSpan retryAfter = blockedUntil - now;
                return CircuitExecutionDecision.Denied(retryAfter);
            }

            // Half-Open: Allow up to N permitted concurrent trial probes (Option C)
            string probeKey = FormatProbeKey(key);
            IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

            CounterLimitResult probeClaim = await probeCounter.TryIncrementAsync(
                amount: 1,
                limit: this._options.PermittedNumberOfCallsInHalfOpenState,
                expiry: CounterExpiry.From(this._options.BreakDuration * 2),
                cancellationToken).ConfigureAwait(false);

            if(probeClaim.IsAllowed) {
                return CircuitExecutionDecision.HalfOpenProbe();
            }

            return CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(1));
        }

        return CircuitExecutionDecision.Allowed();
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        long windowId = GetCurrentWindowId();
        string successKey = FormatSuccessKey(key, windowId);
        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);

        IDistributedCounter successCounter = this._counterFactory.Create(successKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

        CounterExpiry expiry = CounterExpiry.From(this._options.SamplingWindow * 2);
        await successCounter.IncrementAsync(1, expiry, cancellationToken).ConfigureAwait(false);

        // Reset tripped and probe states if recovering from a break
        CounterValue trippedVal = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(trippedVal.Value > 0) {
            await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
            await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        string trippedKey = FormatTrippedKey(key);
        string probeKey = FormatProbeKey(key);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);
        IDistributedCounter probeCounter = this._counterFactory.Create(probeKey);

        // Immediate re-trip if failure happens during half-open trial
        CounterValue currentTrip = await trippedCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);
        if(currentTrip.Value > 0) {
            await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);
            return;
        }

        long windowId = GetCurrentWindowId();
        string successKey = FormatSuccessKey(key, windowId);
        string failureKey = FormatFailureKey(key, windowId);

        IDistributedCounter successCounter = this._counterFactory.Create(successKey);
        IDistributedCounter failureCounter = this._counterFactory.Create(failureKey);

        CounterExpiry expiry = CounterExpiry.From(this._options.SamplingWindow * 2);
        CounterValue newFailureVal = await failureCounter.IncrementAsync(1, expiry, cancellationToken).ConfigureAwait(false);
        CounterValue successVal = await successCounter.GetValueAsync(cancellationToken).ConfigureAwait(false);

        long totalRequests = successVal.Value + newFailureVal.Value;

        if(totalRequests >= this._options.MinimumThroughput) {
            double failureRate = (double)newFailureVal.Value / totalRequests;

            if(failureRate >= this._options.FailureRateThreshold) {
                await TripAsync(key, trippedCounter, probeCounter, cancellationToken).ConfigureAwait(false);

                this._logger.LogWarning("[SamplingWindow] Circuit breaker TRIPPED to OPEN for key '{Key}'. Failure rate {Rate:P1} exceeded threshold {Threshold:P1} across {Total} requests. Break: {DurationMs:F0}ms.",
                    key, failureRate, this._options.FailureRateThreshold, totalRequests, this._options.BreakDuration.TotalMilliseconds);
            }
        }
    }

    private async ValueTask TripAsync(string key, IDistributedCounter trippedCounter, IDistributedCounter probeCounter, CancellationToken cancellationToken) {
        DateTimeOffset blockedUntil = this._timeProvider.GetUtcNow().Add(this._options.BreakDuration);
        CounterExpiry tripExpiry = CounterExpiry.From(this._options.BreakDuration * 2);

        await trippedCounter.SetAsync(blockedUntil.UtcTicks, tripExpiry, cancellationToken).ConfigureAwait(false);
        await probeCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    private long GetCurrentWindowId() => this._timeProvider.GetUtcNow().UtcTicks / this._options.SamplingWindow.Ticks;
    private static string FormatSuccessKey(string key, long windowId) => $"wh:cb:sw:succ:{key}:{windowId}";
    private static string FormatFailureKey(string key, long windowId) => $"wh:cb:sw:fail:{key}:{windowId}";
    private static string FormatTrippedKey(string key) => $"wh:cb:sw:open:{key}";
    private static string FormatProbeKey(string key) => $"wh:cb:sw:probe:{key}";
}