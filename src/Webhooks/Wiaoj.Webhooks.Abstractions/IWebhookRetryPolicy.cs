namespace Wiaoj.Webhooks;

/// <summary>
/// Defines a strategy for deciding whether a failed webhook delivery attempt should be retried and calculating the delay before the next attempt.
/// </summary>
public interface IWebhookRetryPolicy {
    /// <summary>
    /// Evaluates the delivery context and last result to determine if another delivery attempt should be scheduled.
    /// </summary>
    /// <param name="context">The delivery context containing the endpoint, payload, and attempt history.</param>
    /// <param name="lastResult">The outcome of the most recent delivery attempt.</param>
    /// <param name="nextDelay">When this method returns <see langword="true"/>, contains the calculated delay before the next attempt.</param>
    /// <returns><see langword="true"/> if the webhook should be retried; otherwise, <see langword="false"/>.</returns>
    bool ShouldRetry(WebhookDeliveryContext context, WebhookDeliveryResult lastResult, out TimeSpan nextDelay);
}
