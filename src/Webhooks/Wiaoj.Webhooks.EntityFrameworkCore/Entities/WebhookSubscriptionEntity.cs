using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore;

/// <summary>
/// Row shape of a <see cref="WebhookSubscription"/> in the subscriptions table.
/// </summary>
public sealed class WebhookSubscriptionEntity {
    /// <summary>Gets or sets the subscription identifier (<see cref="WebhookSubscription.Id"/>).</summary>
    public string Id { get; set; } = null!;

    /// <summary>Gets or sets the isolation namespace (<see cref="WebhookSubscription.Namespace"/>).</summary>
    public string Namespace { get; set; } = null!;

    /// <summary>Gets or sets the target endpoint identifier (<see cref="WebhookSubscription.EndpointId"/>).</summary>
    public string EndpointId { get; set; } = null!;

    /// <summary>Gets or sets the event type pattern (<see cref="WebhookSubscription.EventTypePattern"/>).</summary>
    public string EventTypePattern { get; set; } = null!;

    /// <summary>Gets or sets the content filter expression (<see cref="WebhookSubscription.FilterExpression"/>).</summary>
    public string? FilterExpression { get; set; }

    /// <summary>Gets or sets whether the subscription receives events (<see cref="WebhookSubscription.IsEnabled"/>).</summary>
    public bool IsEnabled { get; set; }

    /// <summary>Gets or sets the creation time in UTC ticks (<see cref="WebhookSubscription.CreatedAt"/>).</summary>
    public long CreatedAtTicks { get; set; }

    /// <summary>Gets or sets the description (<see cref="WebhookSubscription.Description"/>).</summary>
    public string? Description { get; set; }
}
