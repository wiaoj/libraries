namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the lightweight handle returned immediately upon dispatching a webhook event.
/// </summary>
public readonly record struct WebhookDeliveryHandle {
    /// <summary>
    /// Gets the unique identifier of the scheduled webhook job.
    /// </summary>
    public WebhookJobId JobId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookDeliveryHandle"/> struct.
    /// </summary>
    /// <param name="jobId">The unique job identifier.</param>
    public WebhookDeliveryHandle(WebhookJobId jobId) {
        this.JobId = jobId;
    }

    /// <inheritdoc/>
    public override string ToString() => this.JobId.ToString();
}
