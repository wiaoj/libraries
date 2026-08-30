using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Wiaoj.Resilience;

/// <summary>
/// A timeout strategy that enforces a constant deadline duration using a <see cref="TimeProvider"/>.
/// </summary>
public sealed class FixedTimeoutStrategy : ITimeoutStrategy {
    private readonly TimeSpan _timeout;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FixedTimeoutStrategy> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedTimeoutStrategy"/> class with a fixed timeout duration.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    public FixedTimeoutStrategy(TimeSpan timeout)
        : this(timeout, TimeProvider.System, NullLogger<FixedTimeoutStrategy>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedTimeoutStrategy"/> class with a fixed timeout and time provider.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="timeProvider">The time provider for deadline calculations.</param>
    public FixedTimeoutStrategy(TimeSpan timeout, TimeProvider timeProvider)
        : this(timeout, timeProvider, NullLogger<FixedTimeoutStrategy>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="FixedTimeoutStrategy"/> class with all parameters.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <param name="timeProvider">The time provider for deadline calculations.</param>
    /// <param name="logger">The logger instance.</param>
    public FixedTimeoutStrategy(
        TimeSpan timeout,
        TimeProvider timeProvider,
        ILogger<FixedTimeoutStrategy> logger) {
        Preca.ThrowIfNegativeOrZero(timeout);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._timeout = timeout;
        this._timeProvider = timeProvider;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<TResult> ExecuteAsync<TResult>(
        string key,
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using ITimer timer = this._timeProvider.CreateTimer(
            static state => ((CancellationTokenSource)state!).Cancel(),
            timeoutCts,
            this._timeout,
            Timeout.InfiniteTimeSpan);

        try {
            return await operation(timeoutCts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested) {
            this._logger.LogWarning("Operation for key '{Key}' timed out after {TimeoutMs}ms.", key, this._timeout.TotalMilliseconds);
            throw new TimeoutException($"The operation for key '{key}' exceeded the configured timeout of {this._timeout.TotalMilliseconds}ms.");
        }
    }

    /// <inheritdoc/>
    public async ValueTask ExecuteAsync(
        string key,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        Preca.ThrowIfNull(operation);

        cancellationToken.ThrowIfCancellationRequested();

        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using ITimer timer = this._timeProvider.CreateTimer(
            static state => ((CancellationTokenSource)state!).Cancel(),
            timeoutCts,
            this._timeout,
            Timeout.InfiniteTimeSpan);

        try {
            await operation(timeoutCts.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested && timeoutCts.IsCancellationRequested) {
            this._logger.LogWarning("Operation for key '{Key}' timed out after {TimeoutMs}ms.", key, this._timeout.TotalMilliseconds);
            throw new TimeoutException($"The operation for key '{key}' exceeded the configured timeout of {this._timeout.TotalMilliseconds}ms.");
        }
    }
}