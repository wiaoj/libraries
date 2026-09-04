using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Retries;

/// <summary>
/// Webhook middleware that evaluates delivery failure results against an <see cref="IWebhookRetryPolicy"/>,
/// scheduling delayed retries via <see cref="IWebhookTransport"/> or recording dead-letter failure.
/// </summary>
public sealed class RetryMiddleware : IWebhookMiddleware {
    private readonly IWebhookRetryPolicy _retryPolicy;
    private readonly IWebhookTransport _transport;
    private readonly ILogger<RetryMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RetryMiddleware"/> class.
    /// </summary>
    /// <param name="retryPolicy">The retry policy used to evaluate retries and backoff delays.</param>
    /// <param name="transport">The transport used to re-enqueue failed jobs for retry.</param>
    /// <param name="logger">The logger instance.</param>
    public RetryMiddleware(
        IWebhookRetryPolicy retryPolicy,
        IWebhookTransport transport,
        ILogger<RetryMiddleware> logger) {
        Preca.ThrowIfNull(retryPolicy);
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(logger);

        this._retryPolicy = retryPolicy;
        this._transport = transport;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        await next(context, cancellationToken);

        if(!context.TryGetResult(out WebhookDeliveryResult? result)) return;

        int currentAttemptNumber = context.AttemptHistory.Count + 1;

        switch(result) {
            case WebhookDeliveryResult.Delivered:
            case WebhookDeliveryResult.Deduplicated:
                // Başarılı durumlar — retry gerekmez
                break;

            case WebhookDeliveryResult.TransientFailure transient:
                if(this._retryPolicy.ShouldRetry(context, transient, out TimeSpan calculatedDelay)) {
                    TimeSpan nextDelay = transient.RetryAfter ?? calculatedDelay; 
                    context.ScheduleRetry(nextDelay);
                    WebhookDeliveryJob retryJob = WebhookDeliveryJob.FromContext(context);
                    await this._transport.EnqueueAsync(retryJob, nextDelay, cancellationToken);

                    WebhookMeter.RetryCount.Add(1, new TagList {
                        { "webhook.endpoint_id", context.GetEndpointId().ToString() },
                        { "webhook.attempt_number", currentAttemptNumber }
                    });

                    this._logger.LogRetryScheduled(currentAttemptNumber, context.Endpoint.Id, nextDelay.TotalMilliseconds);
                }
                else {
                    // Retry hakkı bitti -> Dead Letter
                    context.MarkDeadLettered();
                    WebhookMeter.DeadLetterCount.Add(1, new TagList {
                        { "webhook.endpoint_id", context.GetEndpointId().ToString() },
                        { "webhook.total_attempts", currentAttemptNumber }
                    });

                    this._logger.LogDeliveryPermanentlyFailed(context.Endpoint.Id, currentAttemptNumber);
                }
                break;

            case WebhookDeliveryResult.PermanentFailure:
                // Kalıcı hata -> Asla retry yapma, doğrudan Dead Letter!
                context.MarkDeadLettered();
                WebhookMeter.DeadLetterCount.Add(1, new TagList {
                    { "webhook.endpoint_id", context.Endpoint.Id },
                    { "webhook.total_attempts", currentAttemptNumber }
                });

                this._logger.LogDeliveryPermanentlyFailed(context.Endpoint.Id, currentAttemptNumber);
                break;
        }
    }
}