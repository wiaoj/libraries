namespace Wiaoj.Webhooks;

/// <summary>
/// Carries all state relevant to a single webhook delivery attempt as it flows through the
/// outbound pipeline, from the first <see cref="IWebhookMiddleware"/> to the terminal
/// <see cref="IWebhookDeliverer"/>.
/// </summary>
/// <remarks>
/// Analogous to <c>HttpContext</c> in ASP.NET Core: a single mutable object shared by every
/// step in the pipeline for the duration of one delivery attempt. Middleware may read and
/// write <see cref="Items"/> to pass data downstream (e.g. a signing middleware attaching a
/// computed signature header for the deliverer to send).
/// </remarks>
public sealed class WebhookDeliveryContext {
    /// <summary>
    /// The unique identifier of the delivery job being processed.
    /// </summary>
    public required WebhookJobId JobId { get; init; }

    /// <summary>
    /// The endpoint this delivery is being sent to.
    /// </summary>
    public required WebhookEndpoint Endpoint { get; init; }

    /// <summary>
    /// The event being delivered.
    /// </summary>
    public required IWebhookEvent Event { get; init; }

    /// <summary>
    /// The serialized (JSON) form of <see cref="Event"/> that will be sent as the request body.
    /// </summary>
    /// <remarks>
    /// Populated before the pipeline runs by the configured payload serializer, so that
    /// middleware (e.g. signing) can operate on the exact bytes that will be transmitted.
    /// </remarks>
    public required string SerializedPayload { get; init; }

    /// <summary>
    /// The target URL this delivery should be sent to.
    /// </summary>
    /// <remarks>
    /// Convenience accessor over <see cref="Endpoint"/>'s target URL. Exposed separately so
    /// deliverers depend on the narrowest possible surface.
    /// </remarks>
    public Uri TargetUrl => this.Endpoint.TargetUrl;

    /// <summary>
    /// The history of attempts made so far for this delivery, oldest first.
    /// </summary>
    public required IReadOnlyList<WebhookDeliveryAttempt> AttemptHistory { get; init; }

    /// <summary>
    /// Arbitrary state that middleware can use to pass data to later steps in the pipeline,
    /// including the terminal <see cref="IWebhookDeliverer"/>.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
}