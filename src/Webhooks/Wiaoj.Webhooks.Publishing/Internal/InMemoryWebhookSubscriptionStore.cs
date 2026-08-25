using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Publishing.Internal;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWebhookSubscriptionStore"/> for single-node deployments and testing.
/// </summary>
internal sealed class InMemoryWebhookSubscriptionStore : IWebhookSubscriptionStore {
    private readonly ConcurrentDictionary<WebhookSubscriptionId, WebhookSubscription> _subscriptions = new();

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(WebhookNamespace @namespace, CancellationToken cancellationToken = default) {
        List<WebhookSubscription> active = this._subscriptions.Values
            .Where(s => s.IsEnabled && s.Namespace.Equals(@namespace))
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<WebhookSubscription>>(active);
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default) {
        return GetActiveSubscriptionsAsync(WebhookNamespace.Default, cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(subscription);
        this._subscriptions[subscription.Id] = subscription;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask DeleteSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
        this._subscriptions.TryRemove(subscriptionId, out _);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<WebhookSubscription?> GetSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
        this._subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? sub);
        return ValueTask.FromResult(sub);
    }
}