namespace Wiaoj.Webhooks;

/// <summary>
/// Terminal step of the outbound delivery pipeline.
/// </summary>
/// <remarks>
/// Unlike <see cref="IWebhookMiddleware"/>, a deliverer does not call a <c>next</c> delegate —
/// it is always the last step in the pipeline, and it is mandatory rather than optional.
/// Exactly one deliverer is configured per pipeline; it is responsible for performing the
/// actual transmission of a webhook (typically an HTTP POST) and translating the outcome
/// into a <see cref="WebhookDeliveryResult"/>.
/// </remarks>
public interface IWebhookDeliverer {

    /// <summary>
    /// Delivers the webhook described by <paramref name="context"/> to its target.
    /// </summary>
    /// <param name="context">The delivery context, including target, payload, and pipeline-collected state.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="WebhookDeliveryResult"/> describing the outcome of the delivery attempt.</returns>
    Task<WebhookDeliveryResult> DeliverAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default);
}