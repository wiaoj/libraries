namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Extension methods for <see cref="IWebhookPublisher"/> providing convenient overloads for default namespace publishing.
/// </summary>
public static class WebhookPublisherExtensions {
    /// <summary>
    /// Publishes a domain event within the default global namespace (<see cref="WebhookNamespace.Default"/>)
    /// using an explicit partition routing key for global FIFO sequencing.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event payload implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="gateway">The gateway instance.</param>
    /// <param name="payload">The strongly-typed domain event payload instance to be published.</param>
    /// <param name="partitionKey">The explicit partition routing key used for strict FIFO sequencing across all matched subscribers.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a read-only list of <see cref="WebhookDeliveryHandle"/> instances for each matched subscriber.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gateway"/> or <paramref name="payload"/> is <see langword="null"/>.</exception>
    public static Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        this IWebhookPublisher gateway,
        TEvent payload,
        WebhookPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(gateway);
        return gateway.PublishAsync(WebhookNamespace.Default, payload, partitionKey, cancellationToken);
    }

    /// <summary>
    /// Publishes a domain event within the default global namespace (<see cref="WebhookNamespace.Default"/>),
    /// defaulting each subscriber's partition key to its own unique endpoint identifier.
    /// </summary>
    /// <typeparam name="TEvent">The type of the domain event payload implementing <see cref="IWebhookEvent"/>.</typeparam>
    /// <param name="gateway">The gateway instance.</param>
    /// <param name="payload">The strongly-typed domain event payload instance to be published.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation, containing a read-only list of <see cref="WebhookDeliveryHandle"/> instances for each matched subscriber.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="gateway"/> or <paramref name="payload"/> is <see langword="null"/>.</exception>
    public static Task<IReadOnlyList<WebhookDeliveryHandle>> PublishAsync<TEvent>(
        this IWebhookPublisher gateway,
        TEvent payload,
        CancellationToken cancellationToken = default)
        where TEvent : IWebhookEvent {
        Preca.ThrowIfNull(gateway);
        return gateway.PublishAsync(WebhookNamespace.Default, payload, cancellationToken);
    }
}