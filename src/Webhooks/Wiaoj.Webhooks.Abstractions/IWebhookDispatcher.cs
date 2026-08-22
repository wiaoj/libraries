namespace Wiaoj.Webhooks;

/// <summary>
/// Defines the single entry point for dispatching webhook events.
/// </summary>
public interface IWebhookDispatcher {
    /// <summary>
    /// Dispatches a webhook event to the specified endpoint, persisting it to the store and enqueuing it onto the execution transport.
    /// </summary>
    /// <typeparam name="TEvent">The type of the event being dispatched.</typeparam>
    /// <param name="endpointId">The identifier of the target endpoint.</param>
    /// <param name="payload">The event payload to dispatch.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="Task{TResult}"/> containing the lightweight <see cref="WebhookDeliveryHandle"/>.</returns>
    Task<WebhookDeliveryHandle> DispatchAsync<TEvent>(WebhookEndpointId endpointId, TEvent payload, CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;

    /// <summary>
    /// Re-enqueues an existing dead-lettered or failed job for immediate reprocessing.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job to replay.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A delivery handle for the replayed job.</returns>
    Task<WebhookDeliveryHandle> ReplayAsync(WebhookJobId jobId, CancellationToken cancellationToken = default);
}