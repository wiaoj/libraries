using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Resilience.Diagnostics;

namespace Wiaoj.Resilience;

/// <summary>
/// What <see cref="ResilientCircuitBreaker.GetStateAsync"/> reports when the circuit's store cannot be read.
/// </summary>
public enum StorageFailureState {
    /// <summary>
    /// Report <see cref="CircuitState.Closed"/>. For routing: a candidate is not dropped because its circuit could not be
    /// read, since nothing is known about the target being unhealthy.
    /// </summary>
    Closed,

    /// <summary>
    /// Rethrow the store's exception. For a health page, which must say "unknown" rather than claim "healthy".
    /// </summary>
    Throw
}

/// <summary>
/// Options for <see cref="ResilientCircuitBreaker"/>.
/// </summary>
public sealed class ResilientCircuitBreakerOptions {
    /// <summary>
    /// Gets or sets what <see cref="ResilientCircuitBreaker.GetStateAsync"/> reports when the store cannot be read.
    /// Defaults to <see cref="StorageFailureState.Closed"/>.
    /// </summary>
    public StorageFailureState StateOnStorageFailure { get; set; } = StorageFailureState.Closed;
}

/// <summary>
/// A circuit breaker decorator that steps aside when the circuit's own store fails, instead of failing the calls it protects.
/// </summary>
/// <remarks>
/// <para>
/// A circuit breaker protects a downstream. When its state lives in a distributed store and that store is unreachable,
/// nothing is known about the downstream — only about the breaker's bookkeeping. Refusing every call in that situation
/// turns an outage of the store into an outage of everything behind every breaker. This is the counterpart of
/// <c>ResilientRateLimiter</c>, for the same reason: a failure in a protection mechanism must not become a failure in
/// the thing being protected.
/// </para>
/// <list type="table">
///   <listheader><term>Member</term><description>When the store throws</description></listheader>
///   <item><term><see cref="TryAcquireAsync"/></term><description>Allows the call, and logs.</description></item>
///   <item><term><see cref="GetStateAsync"/></term><description>Reports <see cref="CircuitState.Closed"/>, or rethrows — see <see cref="ResilientCircuitBreakerOptions.StateOnStorageFailure"/>.</description></item>
///   <item><term><see cref="OnSuccessAsync"/>, <see cref="OnFailureAsync"/></term><description>Logs and returns: bookkeeping about a call that already happened.</description></item>
/// </list>
/// <para>
/// Cancellation the caller requested is always propagated. An <see cref="OperationCanceledException"/> the caller did
/// not request — a store client's own timeout — is treated as a store failure.
/// </para>
/// <para>
/// Any exception from the inner breaker is treated as a store failure; the built-in breakers do nothing else that
/// throws. Register it for every policy with <c>FailOpenOnStorageFailure()</c> on the resilience builder.
/// </para>
/// </remarks>
public sealed class ResilientCircuitBreaker : ICircuitBreaker {
    private readonly ICircuitBreaker _inner;
    private readonly ResilientCircuitBreakerOptions _options;
    private readonly ILogger _logger;
    private readonly string _strategyName;

    /// <summary>Initializes a new instance guarding <paramref name="inner"/>.</summary>
    /// <param name="inner">The circuit breaker whose store failures should not fail calls.</param>
    public ResilientCircuitBreaker(ICircuitBreaker inner)
        : this(inner, new ResilientCircuitBreakerOptions(), NullLogger<ResilientCircuitBreaker>.Instance) { }

    /// <summary>Initializes a new instance guarding <paramref name="inner"/>.</summary>
    /// <param name="inner">The circuit breaker whose store failures should not fail calls.</param>
    /// <param name="options">How a store failure is reported by <see cref="GetStateAsync"/>.</param>
    /// <param name="logger">Receives a warning for every store failure absorbed.</param>
    public ResilientCircuitBreaker(ICircuitBreaker inner, ResilientCircuitBreakerOptions options, ILogger<ResilientCircuitBreaker> logger) {
        Preca.ThrowIfNull(inner);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._inner = inner;
        this._options = options;
        this._logger = logger;
        this._strategyName = inner.GetType().Name;
    }

    /// <inheritdoc/>
    public async ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
        try {
            return await this._inner.TryAcquireAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception exception) when(exception is not ArgumentException) {
            this._logger.LogAcquireFailOpen(this._strategyName, key, exception);
            return CircuitExecutionDecision.Allowed();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) {
        try {
            return await this._inner.GetStateAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception exception) when(exception is not ArgumentException && this._options.StateOnStorageFailure == StorageFailureState.Closed) {
            this._logger.LogStateFailOpen(this._strategyName, key, CircuitState.Closed, exception);
            return CircuitState.Closed;
        }
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        try {
            await this._inner.OnSuccessAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception exception) when(exception is not ArgumentException) {
            this._logger.LogRecordSkipped(this._strategyName, key, "success", exception);
        }
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        try {
            await this._inner.OnFailureAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch(Exception exception) when(exception is not ArgumentException) {
            this._logger.LogRecordSkipped(this._strategyName, key, "failure", exception);
        }
    }
}
