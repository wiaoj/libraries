using Microsoft.Extensions.Logging;
using Wiaoj.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.Resilience;
using Wiaoj.Webhooks.Resilience.Diagnostics;

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

        // 1. Evaluate circuit breaker state before network I/O
        CircuitExecutionDecision decision = await this._circuitBreaker
            .TryAcquireAsync(endpointKey, cancellationToken)
            .ConfigureAwait(false);

        if(!decision.IsAllowed) {
            TimeSpan retryAfter = decision.RetryAfter.ToPositiveOrDefault(1.Seconds());
            this._logger.LogCircuitBreakerOpenFastFailed(endpointKey, retryAfter.TotalMilliseconds);

            context.SetResult(WebhookDeliveryResult.CircuitBroken(endpointKey, retryAfter));
            return;
        }

        // 2. Execute downstream pipeline (Signing -> Deliverer -> Retry)
        try {
            await next(context, cancellationToken).ConfigureAwait(false);
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            throw;
        }
        catch {
            await this._circuitBreaker.OnFailureAsync(endpointKey, cancellationToken).ConfigureAwait(false);
            throw;
        }

        // 3. Update circuit breaker state based on outcome
        if(context.HasSuccessResult()) {
            await this._circuitBreaker.OnSuccessAsync(endpointKey, cancellationToken).ConfigureAwait(false);
        }
        else if(context.TryGetResult(out WebhookDeliveryResult? result) && result is WebhookDeliveryResult.TransientFailure) {
            await this._circuitBreaker.OnFailureAsync(endpointKey, cancellationToken).ConfigureAwait(false);
        }
    }
}