using Wiaoj.Abstractions;

namespace Wiaoj.Webhooks;

/// <summary>
/// Carries all mutable state relevant to a single webhook delivery attempt through the outbound pipeline.
/// </summary>
public sealed class WebhookDeliveryContext : ICloneable<WebhookDeliveryContext> {
    /// <summary>Gets the unique identifier of the delivery job currently being executed.</summary>
    public required WebhookJobId JobId { get; init; }

    /// <summary>Gets the destination endpoint configuration.</summary>
    public required WebhookEndpoint Endpoint { get; init; }

    /// <summary>Gets the partition routing key associated with this delivery.</summary>
    public required WebhookPartitionKey PartitionKey { get; init; }

    /// <summary>Gets the canonical wire-format event discriminator name.</summary>
    public required string EventType { get; init; }

    /// <summary>Gets the domain event object being delivered.</summary>
    public required IWebhookEvent Event { get; init; }

    /// <summary>Gets the pre-serialized request payload.</summary>
    public required string SerializedPayload { get; init; }

    /// <summary>Gets the destination URL for this delivery attempt.</summary>
    public Uri TargetUrl => this.Endpoint.TargetUrl;

    /// <summary>Gets the collection of prior delivery attempts executed for this job.</summary>
    public required IReadOnlyList<WebhookDeliveryAttempt> AttemptHistory { get; init; }

    /// <summary>Gets the state dictionary for sharing pipeline metadata.</summary>
    public IDictionary<string, object?> Items { get; init; } = new Dictionary<string, object?>();

    /// <summary>Creates a deep clone isolating mutable dictionary state.</summary>
    public WebhookDeliveryContext DeepClone() {
        Dictionary<string, object?> clonedItems = new(this.Items.Count);
        foreach(KeyValuePair<string, object?> kvp in this.Items) {
            if(kvp.Value is IDictionary<string, string> headers) {
                clonedItems[kvp.Key] = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase);
            }
            else {
                clonedItems[kvp.Key] = kvp.Value;
            }
        }

        return new WebhookDeliveryContext {
            JobId = this.JobId,
            Endpoint = this.Endpoint,
            PartitionKey = this.PartitionKey,
            EventType = this.EventType,
            Event = this.Event,
            SerializedPayload = this.SerializedPayload,
            AttemptHistory = [.. this.AttemptHistory],
            Items = clonedItems
        };
    }

    /// <summary>Creates a shallow clone sharing items dictionary reference.</summary>
    public WebhookDeliveryContext ShallowClone() {
        return new() {
            JobId = this.JobId,
            Endpoint = this.Endpoint,
            PartitionKey = this.PartitionKey,
            EventType = this.EventType,
            Event = this.Event,
            SerializedPayload = this.SerializedPayload,
            AttemptHistory = this.AttemptHistory,
            Items = this.Items
        };
    }
}