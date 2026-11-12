namespace Wiaoj.Webhooks;

/// <summary>
/// One delivery attempt through the configured middleware and deliverer, and nothing around it: no store, no lease, no
/// transport, no retry scheduled.
/// </summary>
/// <remarks>
/// For a host that already has its own queue and its own retry — an outbox, a broker's scheduled redelivery — and wants
/// the engine's sending (headers, digest, signing, the HTTP call and the classification of its answer) without a second
/// queue beside its own. The caller builds the <see cref="WebhookDeliveryContext"/>, including the serialized payload and
/// the attempts it has already made, and decides what a <see cref="WebhookDeliveryResult.TransientFailure"/> means.
/// <see cref="IWebhookJobHandler"/> is the same attempt with the engine's store around it.
/// </remarks>
public interface IWebhookDeliveryPipeline {
    /// <summary>Runs one attempt for <paramref name="context"/> and says how it ended.</summary>
    Task<WebhookDeliveryAttempt> RunAsync(WebhookDeliveryContext context, CancellationToken cancellationToken = default);
}
