using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic percentage-based circuit breaker strategy calculating error rates over rolling time windows.
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
            return CircuitExecutionDecision.Denied(this._options.BreakDuration);
        }

        return CircuitExecutionDecision.Allowed();
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        long windowId = GetCurrentWindowId();
        string successKey = FormatSuccessKey(key, windowId);
        string trippedKey = FormatTrippedKey(key);

        IDistributedCounter successCounter = this._counterFactory.Create(successKey);
        IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);

        CounterExpiry expiry = CounterExpiry.From(this._options.SamplingWindow * 2);
        await successCounter.IncrementAsync(1, expiry, cancellationToken).ConfigureAwait(false);
        await trippedCounter.ResetAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

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
                string trippedKey = FormatTrippedKey(key);
                IDistributedCounter trippedCounter = this._counterFactory.Create(trippedKey);

                CounterExpiry tripExpiry = CounterExpiry.From(this._options.BreakDuration);
                await trippedCounter.IncrementAsync(1, tripExpiry, cancellationToken).ConfigureAwait(false);

                this._logger.LogWarning("[SamplingWindow] Circuit breaker TRIPPED to OPEN for key '{Key}'. Failure rate {Rate:P1} exceeded threshold {Threshold:P1} across {Total} requests. Break: {DurationMs:F0}ms.",
                    key, failureRate, this._options.FailureRateThreshold, totalRequests, this._options.BreakDuration.TotalMilliseconds);
            }
        }
    }

    private long GetCurrentWindowId() {
        return this._timeProvider.GetUtcNow().UtcTicks / this._options.SamplingWindow.Ticks;
    }

    private static string FormatSuccessKey(string key, long windowId) => $"wh:cb:sw:succ:{key}:{windowId}";
    private static string FormatFailureKey(string key, long windowId) => $"wh:cb:sw:fail:{key}:{windowId}";
    private static string FormatTrippedKey(string key) => $"wh:cb:sw:open:{key}";
}