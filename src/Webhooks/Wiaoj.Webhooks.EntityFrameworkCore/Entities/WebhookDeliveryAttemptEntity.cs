namespace Wiaoj.Webhooks.EntityFrameworkCore;

/// <summary>
/// Row shape of a <see cref="WebhookDeliveryAttempt"/> in the attempts table, one row per attempt of a job.
/// </summary>
/// <remarks>
/// The polymorphic <see cref="WebhookDeliveryResult"/> is flattened into <see cref="ResultKind"/> and the nullable
/// columns its variants use. <see cref="WebhookDeliveryResult.TransientFailure.Exception"/> is not persisted: it is a
/// live object of the attempt that failed, and its message is already part of the error message.
/// </remarks>
public sealed class WebhookDeliveryAttemptEntity {
    /// <summary>Gets or sets the database-generated identifier, which also orders a job's attempts.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the identifier of the job the attempt belongs to.</summary>
    public string JobId { get; set; } = null!;

    /// <summary>Gets or sets the endpoint identifier (<see cref="WebhookDeliveryAttempt.EndpointId"/>).</summary>
    public string EndpointId { get; set; } = null!;

    /// <summary>Gets or sets the one-based attempt number (<see cref="WebhookDeliveryAttempt.AttemptNumber"/>).</summary>
    public int AttemptNumber { get; set; }

    /// <summary>Gets or sets the attempt time in Unix milliseconds (<see cref="WebhookDeliveryAttempt.AttemptedAt"/>).</summary>
    public long AttemptedAtUnixMilliseconds { get; set; }

    /// <summary>Gets or sets the attempt duration in ticks (<see cref="WebhookDeliveryAttempt.Duration"/>).</summary>
    public long DurationTicks { get; set; }

    /// <summary>Gets or sets whether the attempt was a replay (<see cref="WebhookDeliveryAttempt.IsReplay"/>).</summary>
    public bool IsReplay { get; set; }

    /// <summary>
    /// Gets or sets the result variant: <c>delivered</c>, <c>deduplicated</c>, <c>transient_failure</c> or
    /// <c>permanent_failure</c>, the same discriminators <see cref="WebhookDeliveryResult"/> uses in JSON.
    /// </summary>
    public string ResultKind { get; set; } = null!;

    /// <summary>Gets or sets the status code of a delivered or failed attempt.</summary>
    public int? StatusCode { get; set; }

    /// <summary>Gets or sets the response body of a delivered attempt.</summary>
    public string? ResponseBody { get; set; }

    /// <summary>Gets or sets the deduplication key of a deduplicated attempt.</summary>
    public string? DeduplicationKey { get; set; }

    /// <summary>Gets or sets the error message of a failed attempt.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the retry-after hint of a transient failure, in ticks.</summary>
    public long? RetryAfterTicks { get; set; }

    /// <summary>
    /// Gets or sets the failure reason: a <see cref="TransientFailureReason"/> or a <see cref="PermanentFailureReason"/>,
    /// depending on <see cref="ResultKind"/>.
    /// </summary>
    public int? FailureReason { get; set; }
}
