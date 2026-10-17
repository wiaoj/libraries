using Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Contracts;

/// <summary>
/// The behaviour every <see cref="IWebhookSubscriptionStore"/> must have, written once and run against each backend.
/// </summary>
public abstract class WebhookSubscriptionStoreContractTests : StoreTestBase {
    private WebhookSubscription NewSubscription(string @namespace = "default", string endpoint = "ep-1", string pattern = "order.*") {
        return new WebhookSubscription(
            WebhookSubscriptionId.NewId(),
            new WebhookNamespace(@namespace),
            new WebhookEndpointId(endpoint),
            pattern,
            this.Time.GetUtcNow()) {
            FilterExpression = "$.total > 100",
            Description = "orders over 100"
        };
    }

    [Fact]
    public async Task SaveSubscriptionAsync_ThenGetSubscriptionAsync_ReturnsIt() {
        WebhookSubscription subscription = NewSubscription("tenant-a", "ep-9", "invoice.paid");
        await this.SubscriptionStore.SaveSubscriptionAsync(subscription, TestContext.Current.CancellationToken);

        WebhookSubscription? loaded = await this.SubscriptionStore.GetSubscriptionAsync(subscription.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(subscription.Id, loaded.Id);
        Assert.Equal(subscription.Namespace, loaded.Namespace);
        Assert.Equal(subscription.EndpointId, loaded.EndpointId);
        Assert.Equal(subscription.EventTypePattern, loaded.EventTypePattern);
        Assert.Equal(subscription.FilterExpression, loaded.FilterExpression);
        Assert.Equal(subscription.Description, loaded.Description);
        Assert.Equal(subscription.CreatedAt, loaded.CreatedAt);
        Assert.True(loaded.IsEnabled);
    }

    [Fact]
    public async Task GetSubscriptionAsync_Unknown_ReturnsNull() {
        Assert.Null(await this.SubscriptionStore.GetSubscriptionAsync(WebhookSubscriptionId.NewId(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveSubscriptionAsync_Again_ReplacesTheStoredSubscription() {
        WebhookSubscription subscription = NewSubscription();
        await this.SubscriptionStore.SaveSubscriptionAsync(subscription, TestContext.Current.CancellationToken);

        WebhookSubscription changed = new(subscription.Id, subscription.Namespace, subscription.EndpointId, subscription.EventTypePattern, subscription.CreatedAt) {
            IsEnabled = false,
            Description = "paused"
        };
        await this.SubscriptionStore.SaveSubscriptionAsync(changed, TestContext.Current.CancellationToken);

        WebhookSubscription? loaded = await this.SubscriptionStore.GetSubscriptionAsync(subscription.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(loaded);
        Assert.False(loaded.IsEnabled);
        Assert.Equal("paused", loaded.Description);
        Assert.Empty(await this.SubscriptionStore.GetActiveSubscriptionsAsync(subscription.Namespace, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_ReturnsEnabledSubscriptionsOfTheNamespaceOnly() {
        WebhookSubscription tenantA = NewSubscription("tenant-a");
        WebhookSubscription tenantB = NewSubscription("tenant-b");
        WebhookSubscription disabled = NewSubscription("tenant-a");
        disabled.IsEnabled = false;

        foreach(WebhookSubscription s in new[] { tenantA, tenantB, disabled }) {
            await this.SubscriptionStore.SaveSubscriptionAsync(s, TestContext.Current.CancellationToken);
        }

        IReadOnlyList<WebhookSubscription> active = await this.SubscriptionStore.GetActiveSubscriptionsAsync(new WebhookNamespace("tenant-a"), TestContext.Current.CancellationToken);

        Assert.Equal([tenantA.Id], active.Select(s => s.Id));
    }

    [Fact]
    public async Task GetActiveSubscriptionsAsync_WithoutNamespace_UsesTheDefaultNamespace() {
        WebhookSubscription inDefault = NewSubscription(WebhookNamespace.Default.Value);
        await this.SubscriptionStore.SaveSubscriptionAsync(inDefault, TestContext.Current.CancellationToken);
        await this.SubscriptionStore.SaveSubscriptionAsync(NewSubscription("tenant-a"), TestContext.Current.CancellationToken);

        IReadOnlyList<WebhookSubscription> active = await this.SubscriptionStore.GetActiveSubscriptionsAsync(TestContext.Current.CancellationToken);

        Assert.Equal([inDefault.Id], active.Select(s => s.Id));
    }

    [Fact]
    public async Task DeleteSubscriptionAsync_RemovesIt_AndIgnoresUnknownIds() {
        WebhookSubscription subscription = NewSubscription();
        await this.SubscriptionStore.SaveSubscriptionAsync(subscription, TestContext.Current.CancellationToken);

        await this.SubscriptionStore.DeleteSubscriptionAsync(subscription.Id, TestContext.Current.CancellationToken);
        await this.SubscriptionStore.DeleteSubscriptionAsync(WebhookSubscriptionId.NewId(), TestContext.Current.CancellationToken);

        Assert.Null(await this.SubscriptionStore.GetSubscriptionAsync(subscription.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await this.SubscriptionStore.GetActiveSubscriptionsAsync(TestContext.Current.CancellationToken));
    }
}

public sealed class InMemoryWebhookSubscriptionStoreContractTests : WebhookSubscriptionStoreContractTests {
    protected override StoreBackend CreateBackend() => new InMemoryStoreBackend();
}

public sealed class SqliteWebhookSubscriptionStoreContractTests : WebhookSubscriptionStoreContractTests {
    protected override StoreBackend CreateBackend() => new SqliteStoreBackend();
}

public sealed class PostgreSqlWebhookSubscriptionStoreContractTests : WebhookSubscriptionStoreContractTests {
    protected override StoreBackend CreateBackend() => new PostgreSqlStoreBackend();
}
