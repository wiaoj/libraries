namespace Wiaoj.Webhooks;

/// <summary>
/// Well-known keys used across the framework when reading or writing to <see cref="WebhookDeliveryContext.Items"/>.
/// </summary>
public static class WebhookDeliveryContextItemKeys {
    /// <summary>
    /// Key under which the terminal deliverer's <see cref="WebhookDeliveryResult"/> is stored,
    /// so the pipeline runner can read it back after the pipeline completes.
    /// </summary>
    public const string Result = "__wiaoj.webhooks.result";

    /// <summary>
    /// Key under which outbound HTTP headers dictionary is stored in <see cref="WebhookDeliveryContext.Items"/>.
    /// </summary>
    public const string Headers = "__wiaoj.webhooks.headers";

    /// <summary>
    /// Key under which the computed <see cref="WebhookSignature"/> is stored in <see cref="WebhookDeliveryContext.Items"/>.
    /// </summary>
    public const string Signature = "__wiaoj.webhooks.signature";

    /// <summary>
    /// Key under which a boolean flag (<see cref="bool"/>) is stored in <see cref="WebhookDeliveryContext.Items"/> indicating
    /// that the webhook delivery attempt has permanently failed — either because it exhausted its maximum configured retry budget 
    /// or encountered a non-retryable <see cref="WebhookDeliveryResult.PermanentFailure"/> — signaling to the job handler 
    /// and persistent store that the job lifecycle status must transition to <see cref="WebhookJobStatus.DeadLettered"/>.
    /// </summary>
    public const string IsDeadLettered = "__wiaoj.webhooks.is_dead_lettered";
}