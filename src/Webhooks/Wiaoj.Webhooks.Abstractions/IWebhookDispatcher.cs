namespace Wiaoj.Webhooks;

/// <summary>
/// Defines the single entry point for dispatching webhook events.
/// </summary>
public interface IWebhookDispatcher {
    /// <summary>
    /// Dispatches a webhook event to the specified endpoint with an explicit partition key for FIFO ordering.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being dispatched.</typeparam>
    /// <param name="endpointId">The identifier of the target endpoint.</param>
    /// <param name="payload">The event payload to dispatch.</param>
    /// <param name="partitionKey">The partition key (e.g. OrderId, CustomerId, TenantId, or EndpointId).</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A delivery handle containing the scheduled job identifier.</returns>
    Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        WebhookEndpointId endpointId,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;

    /// <summary>
    /// Re-enqueues an existing dead-lettered or failed job for immediate reprocessing.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to replay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A delivery handle for the replayed job.</returns>
    Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Extension methods for <see cref="IWebhookDispatcher"/> providing convenient dispatch overloads.
/// </summary>
public static class WebhookDispatcherExtensions {
    /// <summary>
    /// Dispatches a webhook event defaulting the partition key to the target <paramref name="endpointId"/>.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being dispatched.</typeparam>
    /// <param name="dispatcher">The dispatcher instance.</param>
    /// <param name="endpointId">The identifier of the target endpoint.</param>
    /// <param name="payload">The event payload to dispatch.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A delivery handle containing the scheduled job identifier.</returns>
    public static Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(
        this IWebhookDispatcher dispatcher,
        WebhookEndpointId endpointId,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(dispatcher);
        return dispatcher.DispatchAsync(endpointId, payload, WebhookPartitionKey.From(endpointId), cancellationToken);
    }
}