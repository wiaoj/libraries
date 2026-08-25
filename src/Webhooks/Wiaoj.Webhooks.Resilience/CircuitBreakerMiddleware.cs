using Microsoft.Extensions.Logging;
using Wiaoj.Preconditions;
using Wiaoj.Resilience;
using Wiaoj.Resilience.CircuitBreaker;

namespace Wiaoj.Webhooks.Resilience;

/// <summary>
/// Webhook delivery middleware that shields destination endpoints and internal worker pools
/// by intercepting calls to failing endpoints using a circuit breaker state machine.
/// </summary>
public sealed class CircuitBreakerMiddleware : IWebhookMiddleware {
    private readonly ICircuitBreakerStore _store;
    private readonly CircuitBreakerOptions _options;
    private readonly ILogger<CircuitBreakerMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerMiddleware"/> class.
    /// </summary>
    /// <param name="store">The underlying circuit breaker state store.</param>
    /// <param name="options">The circuit breaker configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public CircuitBreakerMiddleware(
        ICircuitBreakerStore store,
        CircuitBreakerOptions options,
        ILogger<CircuitBreakerMiddleware> logger) {
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        options.Validate();
        this._store = store;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string endpointKey = context.Endpoint.Id.Value;

        // 1. Evaluate circuit state machine before touching network sockets
        CircuitExecutionDecision decision = await this._store.CanExecuteAsync(endpointKey, cancellationToken).ConfigureAwait(false);

        if(!decision.IsAllowed) {
            TimeSpan retryAfter = decision.RetryAfter ?? this._options.BreakDuration;
            this._logger.LogWarning("Circuit breaker is OPEN for endpoint '{EndpointId}'. Fast-failing delivery and re-enqueuing with delay {RetryAfterMs:F0}ms.",
                endpointKey, retryAfter.TotalMilliseconds);

            // Fast-Fail: Shield target server and re-enqueue delivery as transient failure
            context.SetResult(WebhookDeliveryResult.Transient(
                $"Circuit breaker is OPEN for endpoint '{endpointKey}'.",
                statusCode: 503,
                retryAfter: retryAfter));
            return;
        }

        // 2. Execute downstream pipeline (Signing -> HTTP Delivery -> Retry)
        await next(context, cancellationToken).ConfigureAwait(false);

        // 3. Update state machine based on downstream outcome
        if(context.HasSuccessResult()) {
            await this._store.RecordSuccessAsync(endpointKey, cancellationToken).ConfigureAwait(false);
        }
        else if(context.TryGetResult(out WebhookDeliveryResult? result) && result is WebhookDeliveryResult.TransientFailure) {
            // Only transient failures (5xx, timeout, network glitch) trip the circuit; permanent 4xx are ignored!
            await this._store.RecordFailureAsync(endpointKey, this._options, cancellationToken).ConfigureAwait(false);
        }
    }
}