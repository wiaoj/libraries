using Wiaoj.Webhooks.Publishing.Internal;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "SubscriptionStore")]
public sealed class InMemoryWebhookSubscriptionStoreTests {
    private readonly InMemoryWebhookSubscriptionStore _store = new();

    [Fact]
    public async Task SaveAndGet_RetrievesStoredSubscriptionSuccessfully() {
        // Arrange
        WebhookSubscriptionId id = WebhookSubscriptionId.NewId();
        WebhookEndpointId endpointId = new("ep-store-test");
        WebhookSubscription subscription = new(id, endpointId, "order.*") {
            Description = "Accounting service subscription"
        };

        // Act
        await this._store.SaveSubscriptionAsync(subscription);
        WebhookSubscription? retrieved = await this._store.GetSubscriptionAsync(id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved.Id);
        Assert.Equal(endpointId, retrieved.EndpointId);
        Assert.Equal("order.*", retrieved.EventTypePattern);
        Assert.Equal("Accounting service subscription", retrieved.Description);
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_RemovesSubscriptionFromStore() {
        // Arrange
        WebhookSubscription subscription = new(new WebhookEndpointId("ep-delete"), "invoice.*");
        await this._store.SaveSubscriptionAsync(subscription);

        // Act
        await this._store.DeleteSubscriptionAsync(subscription.Id);
        WebhookSubscription? retrieved = await this._store.GetSubscriptionAsync(subscription.Id);
        IReadOnlyList<WebhookSubscription> active = await this._store.GetActiveSubscriptionsAsync();

        // Assert
        Assert.Null(retrieved);
        Assert.Empty(active);
    }

    [Fact]
    public async Task GetSubscriptionAsync_ReturnsNull_WhenIdNotFound() {
        // Act
        WebhookSubscription? result = await this._store.GetSubscriptionAsync(WebhookSubscriptionId.NewId());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Store_UnderConcurrentReadsAndWrites_MaintainsDataIntegrity() {
        // Arrange: 100 concurrent tasks performing mixed save, read, and delete operations
        Task[] tasks = Enumerable.Range(0, 100).Select(async i => {
            WebhookSubscription sub = new(new WebhookEndpointId($"ep-{i}"), $"event.{i}");
            await this._store.SaveSubscriptionAsync(sub);

            WebhookSubscription? fetched = await this._store.GetSubscriptionAsync(sub.Id);
            Assert.NotNull(fetched);

            if(i % 2 == 0) {
                await this._store.DeleteSubscriptionAsync(sub.Id);
            }
        }).ToArray();

        // Act
        await Task.WhenAll(tasks);

        // Assert: 50 subscriptions must remain active
        IReadOnlyList<WebhookSubscription> active = await this._store.GetActiveSubscriptionsAsync();
        Assert.Equal(50, active.Count);
    }
}