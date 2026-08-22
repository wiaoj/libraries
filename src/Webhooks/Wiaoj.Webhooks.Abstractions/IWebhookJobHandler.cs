namespace Wiaoj.Webhooks;

/// <summary>
/// Executes the full processing logic for a single dequeued <see cref="WebhookDeliveryJob"/>:
/// resolving its endpoint, running it through the outbound pipeline, and recording the
/// resulting attempt.
/// </summary>
public interface IWebhookJobHandler {
    /// <summary>
    /// Processes a single dequeued job to completion.
    /// </summary>
    /// <param name="job">The job to process.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>The <see cref="WebhookDeliveryAttempt"/> describing the outcome.</returns>
    Task<WebhookDeliveryAttempt> HandleAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default);
}