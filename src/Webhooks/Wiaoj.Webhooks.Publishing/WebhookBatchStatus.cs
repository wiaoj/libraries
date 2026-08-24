using System.Text.Json.Serialization;

namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Represents the execution lifecycle status of a 1-to-N webhook publish batch.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<WebhookBatchStatus>))]
public enum WebhookBatchStatus {
    /// <summary>The batch is registered and pending initial fan-out execution.</summary>
    Pending = 0,

    /// <summary>The batch is actively dispatching jobs to matched subscriber endpoints.</summary>
    InFlight = 1,

    /// <summary>All target subscriber endpoints received their dispatch jobs successfully.</summary>
    Completed = 2,

    /// <summary>The batch was interrupted midway; remaining subscribers will be processed by recovery.</summary>
    PartiallyCompleted = 3,

    /// <summary>The batch failed permanently due to fatal infrastructure errors.</summary>
    Failed = 4
}