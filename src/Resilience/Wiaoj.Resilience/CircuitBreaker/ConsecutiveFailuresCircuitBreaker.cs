using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Internal;

namespace Wiaoj.Resilience;

/// <summary>
/// Implements an atomic circuit breaker strategy that trips to <see cref="CircuitState.Open"/>
/// when consecutive transient failures reach a configured threshold.
/// </summary>
/// <remarks>
/// This class is a thin, options-typed façade over <see cref="DistributedCircuitBreakerStore"/>.
/// It used to re-implement the entire trip/half-open/retry state machine independently against
/// <see cref="IDistributedCounterFactory"/> directly - which meant every fix made to the store
/// (atomic single-probe claiming on Half-Open, precise <c>RetryAfter</c> via a stored absolute
/// "blocked until" timestamp instead of always returning the full <see cref="CircuitBreakerOptions.BreakDuration"/>)
/// had to be made twice, and in practice only ever got made once. Delegating here means there is
/// exactly one implementation of "consecutive failure" circuit breaking to reason about and test.
/// <see cref="SamplingWindowCircuitBreaker"/> is intentionally NOT refactored this way - its
/// windowed failure-rate algorithm is genuinely different (multiple rolling counters, no single
/// "consecutive failure count") and doesn't map onto the store's key shape.
/// </remarks>
public sealed class ConsecutiveFailuresCircuitBreaker : ICircuitBreaker {
    private readonly DistributedCircuitBreakerStore _store;
    private readonly CircuitBreakerOptions _options;

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
        this._options = options;

        // KNOWN LIMITATION: the store's own log lines (trip/re-trip warnings) currently go
        // through a NullLogger instead of the caller-supplied `logger`, because
        // DistributedCircuitBreakerStore requires an ILogger<DistributedCircuitBreakerStore>
        // specifically and this constructor only receives an ILogger<ConsecutiveFailuresCircuitBreaker>.
        // If the consumer's logging pipeline needs to see those trip events, thread an
        // ILoggerFactory through here instead and call CreateLogger<DistributedCircuitBreakerStore>().
        this._store = new DistributedCircuitBreakerStore(
            counterFactory,
            timeProvider,
            NullLogger<DistributedCircuitBreakerStore>.Instance);
    }

    /// <inheritdoc/>
    public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
        return this._store.CanExecuteAsync(key, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        return this._store.RecordSuccessAsync(key, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        return this._store.RecordFailureAsync(key, this._options, cancellationToken);
    }
}