using Microsoft.Extensions.Logging;
using Wiaoj.Preconditions;
using Wiaoj.Resilience;

namespace Wiaoj.Webhooks.Resilience;

/// <summary>
/// Webhook delivery middleware that shields destination endpoints and internal worker pools
/// by intercepting calls to failing endpoints using an <see cref="ICircuitBreaker"/> strategy.
/// </summary>
internal sealed class CircuitBreakerMiddleware : IWebhookMiddleware {
    private readonly ICircuitBreaker _circuitBreaker;
    private readonly ILogger<CircuitBreakerMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitBreakerMiddleware"/> class.
    /// </summary>
    /// <param name="circuitBreaker">The underlying circuit breaker algorithm instance.</param>
    /// <param name="logger">The logger instance.</param>
    public CircuitBreakerMiddleware(
        ICircuitBreaker circuitBreaker,
        ILogger<CircuitBreakerMiddleware> logger) {
        Preca.ThrowIfNull(circuitBreaker);
        Preca.ThrowIfNull(logger);

        this._circuitBreaker = circuitBreaker;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string endpointKey = context.Endpoint.Id.Value;

        // 1. Evaluate circuit breaker state before touching network sockets
        CircuitExecutionDecision decision = await this._circuitBreaker
            .TryAcquireAsync(endpointKey, cancellationToken)
            .ConfigureAwait(false);

        if(!decision.IsAllowed) {
            TimeSpan retryAfter = decision.RetryAfter ?? TimeSpan.FromMinutes(1);
            this._logger.LogWarning("Circuit breaker is OPEN for endpoint '{EndpointId}'. Fast-failing delivery and re-enqueuing with delay {RetryAfterMs:F0}ms.",
                endpointKey, retryAfter.TotalMilliseconds);

            // Fast-Fail: Shield target server and re-enqueue delivery as transient failure

            context.SetResult(WebhookDeliveryResult.CircuitBroken(endpointKey, retryAfter));
            return;
        }

        // 2. Execute downstream pipeline (Signing -> HTTP POST Deliverer -> Retry)
        try {
            await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            // Caller cancellation must never count as a downstream server failure
            throw;
        }
        catch {
            // Unexpected unhandled pipeline exception treated as transient failure
            await this._circuitBreaker.OnFailureAsync(endpointKey, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // 3. Update circuit breaker state based on delivery outcome
        if(context.HasSuccessResult()) {
            await this._circuitBreaker.OnSuccessAsync(endpointKey, cancellationToken).ConfigureAwait(false);
        }
        else if(context.TryGetResult(out WebhookDeliveryResult? result) && result is WebhookDeliveryResult.TransientFailure) {
            // Only transient failures (5xx, timeouts, network glitches) count toward tripping the breaker
            await this._circuitBreaker.OnFailureAsync(endpointKey, cancellationToken).ConfigureAwait(false);
        }
    }
}