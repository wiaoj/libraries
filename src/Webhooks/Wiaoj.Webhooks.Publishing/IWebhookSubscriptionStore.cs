namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Contract for persisting, querying, and managing webhook subscriber registrations across isolation namespaces.
/// </summary>
public interface IWebhookSubscriptionStore {
    /// <summary>
    /// Retrieves all active subscriptions registered within the specified isolation namespace.
    /// </summary>
    /// <param name="namespace">The isolation namespace to filter subscriptions by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only list of matching active subscriptions.</returns>
    ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(WebhookNamespace @namespace, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active subscriptions registered within the default namespace.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A read-only list of active subscriptions in the default namespace.</returns>
    ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default) {
        return GetActiveSubscriptionsAsync(WebhookNamespace.Default, cancellationToken);
    }

    /// <summary>
    /// Persists a new or updated subscription.
    /// </summary>
    /// <param name="subscription">The subscription to persist.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription by its unique identifier.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask DeleteSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single subscription by its identifier.
    /// </summary>
    /// <param name="subscriptionId">The subscription identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    ValueTask<WebhookSubscription?> GetSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default);
}