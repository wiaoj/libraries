namespace Wiaoj.Webhooks;

/// <summary>
/// Represents a single cross-cutting step in the outbound delivery pipeline (e.g. idempotency,
/// rate limiting, signing).
/// </summary>
/// <remarks>
/// Unlike <see cref="IWebhookDeliverer"/>, a middleware calls the next delegate (see
/// <see cref="WebhookDelegate"/>) to continue the pipeline; a middleware that does not call it
/// short-circuits the pipeline. Middleware is optional and composable — none of it is required
/// for a dispatch to complete.
/// </remarks>
public interface IWebhookMiddleware {

    /// <summary>
    /// Executes this middleware's logic and, if it should continue, invokes <paramref name="next"/>.
    /// </summary>
    /// <param name="context">The delivery context shared across the pipeline.</param>
    /// <param name="next">The delegate that continues execution to the next step in the pipeline.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the next step in the webhook outbound pipeline.
/// </summary>
/// <param name="context">The delivery context shared across the pipeline.</param>
/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
public delegate Task WebhookDelegate(WebhookDeliveryContext context, CancellationToken cancellationToken);