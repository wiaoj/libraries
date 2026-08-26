using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Preconditions;
using Wiaoj.Resilience.Diagnostics;

namespace Wiaoj.Resilience;

/// <summary>
/// A composite <see cref="ICircuitBreaker"/> that evaluates multiple circuit breaker tiers in sequence.
/// If any tier is in the <see cref="CircuitState.Open"/> state, execution is denied and subsequent tiers are short-circuited.
/// </summary>
public sealed class CompositeCircuitBreaker : ICircuitBreaker {
    private const string StrategyName = "CompositeCircuitBreaker";
    private readonly IReadOnlyList<ICircuitBreaker> _breakers;
    private readonly ILogger<CompositeCircuitBreaker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCircuitBreaker"/> class.
    /// </summary>
    /// <param name="breakers">The sequence of child circuit breakers to evaluate in order.</param>
    public CompositeCircuitBreaker(params IReadOnlyList<ICircuitBreaker> breakers)
        : this(breakers, NullLogger<CompositeCircuitBreaker>.Instance) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeCircuitBreaker"/> class with diagnostic logging.
    /// </summary>
    /// <param name="breakers">The sequence of child circuit breakers to evaluate in order.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="breakers"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="breakers"/> is empty.</exception>
    public CompositeCircuitBreaker(IReadOnlyList<ICircuitBreaker> breakers, ILogger<CompositeCircuitBreaker> logger) {
        Preca.ThrowIfNull(breakers);
        Preca.ThrowIfNull(logger);

        if(breakers.Count == 0) {
            throw new ArgumentException("Composite circuit breaker requires at least one breaker tier.", nameof(breakers));
        }

        this._breakers = breakers;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);
        cancellationToken.ThrowIfCancellationRequested();

        TimeSpan maxRetryAfter = TimeSpan.Zero;
        bool isHalfOpenProbe = false;

        for(int i = 0; i < this._breakers.Count; i++) {
            CircuitExecutionDecision decision = await this._breakers[i].TryAcquireAsync(key, cancellationToken).ConfigureAwait(false);

            if(!decision.IsAllowed) {
                TimeSpan retryAfter = decision.RetryAfter ?? TimeSpan.FromSeconds(1);
                if(retryAfter > maxRetryAfter) {
                    maxRetryAfter = retryAfter;
                }

                CircuitExecutionDecision deniedDecision = CircuitExecutionDecision.Denied(maxRetryAfter);
                ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, deniedDecision);
                return deniedDecision;
            }

            if(decision.State == CircuitState.HalfOpen) {
                isHalfOpenProbe = true;
            }
        }

        CircuitExecutionDecision allowedDecision = isHalfOpenProbe
            ? CircuitExecutionDecision.HalfOpenProbe()
            : CircuitExecutionDecision.Allowed();

        ResilienceDiagnostics.RecordDecision(this._logger, StrategyName, key, allowedDecision);
        return allowedDecision;
    }

    /// <inheritdoc/>
    public async ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        for(int i = 0; i < this._breakers.Count; i++) {
            await this._breakers[i].OnSuccessAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(key);

        for(int i = 0; i < this._breakers.Count; i++) {
            await this._breakers[i].OnFailureAsync(key, cancellationToken).ConfigureAwait(false);
        }
    }
}