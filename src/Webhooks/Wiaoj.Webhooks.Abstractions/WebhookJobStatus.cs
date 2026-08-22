using System.Text.Json.Serialization;

namespace Wiaoj.Webhooks;

/// <summary>
/// Represents the execution lifecycle status of a webhook delivery job.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WebhookJobStatus>))]
public enum WebhookJobStatus {
    /// <summary>
    /// The job has been persisted and enqueued, awaiting pickup by a worker.
    /// </summary>
    Queued = 0,

    /// <summary>
    /// The job is actively being processed by a worker.
    /// </summary>
    InFlight = 1,

    /// <summary>
    /// The job was successfully delivered to the destination endpoint.
    /// </summary>
    Delivered = 2,

    /// <summary>
    /// The job encountered a transient error and is scheduled for a subsequent retry attempt.
    /// </summary>
    Retrying = 3,

    /// <summary>
    /// The job permanently failed or exhausted its maximum retry budget and has been dead-lettered.
    /// </summary>
    DeadLettered = 4
}
