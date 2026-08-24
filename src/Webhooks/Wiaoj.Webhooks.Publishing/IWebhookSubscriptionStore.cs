namespace Wiaoj.Webhooks.Publishing;

/// <summary>
/// Contract for persisting, querying, and managing webhook subscriber registrations.
/// </summary>
public interface IWebhookSubscriptionStore {
    /// <summary>
    /// Retrieves all active subscriptions registered in the store.
    /// </summary>
    ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new or updated subscription.
    /// </summary>
    ValueTask SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a subscription by its unique identifier.
    /// </summary>
    ValueTask DeleteSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single subscription by its identifier.
    /// </summary>
    ValueTask<WebhookSubscription?> GetSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default);
}