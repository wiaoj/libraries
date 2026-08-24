namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Central 1-to-N webhook event publishing gateway and fan-out broker.
/// Evaluates published domain events against subscriber registries, enforces namespace isolation,
/// and dispatches independent delivery jobs for each matching destination endpoint.
/// </summary>
public interface IWebhookPublisher {
    /// <summary>
    /// Publishes a domain event to all matching active subscriber endpoints within a specific isolation namespace
    /// using an explicit partition routing key for global FIFO sequencing.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event payload implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="namespace">The logical isolation namespace or tenant boundary under which subscribers are registered.</param>
    /// <param name="payload">The strongly-typed domain event payload instance to be published.</param>
    /// <param name="partitionKey">The explicit partition routing key used for strict FIFO sequencing across all matched subscribers.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a read-only list of <see cref="WebhookDeliveryHandle"/> instances for each matched subscriber.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;

    /// <summary>
    /// Publishes a domain event to all matching active subscriber endpoints within a specific isolation namespace,
    /// defaulting each subscriber's partition key to its own unique endpoint identifier.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event payload implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="namespace">The logical isolation namespace or tenant boundary under which subscribers are registered.</param>
    /// <param name="payload">The strongly-typed domain event payload instance to be published.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a read-only list of <see cref="WebhookDeliveryHandle"/> instances for each matched subscriber.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the operation is canceled via <paramref name="cancellationToken"/>.</exception>
    Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        WebhookNamespace @namespace,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent;
}