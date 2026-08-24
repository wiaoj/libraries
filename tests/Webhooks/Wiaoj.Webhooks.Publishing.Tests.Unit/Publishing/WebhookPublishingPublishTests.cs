using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Publishing;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "Publisher")]
public sealed class WebhookPublisherPublishTests {

    [WebhookEvent("order.created")]
    public sealed record OrderCreatedEvent(string OrderId, decimal Amount) : IWebhookEvent;

    [WebhookEvent("payment.captured")]
    public sealed record PaymentCapturedEvent(string PaymentId) : IWebhookEvent;

    private static (WebhookPublisher Gateway, InMemoryWebhookSubscriptionStore Store, FakeWebhookDispatcher Dispatcher) CreateSut() {
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new(); 

        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        return (gateway, store, dispatcher);
    }

    [Fact]
    public async Task PublishAsync_FansOutEvent_ToAllMatchingSubscribers() {
        // Arrange
        (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

        WebhookEndpointId ep1 = new("accounting-service");
        WebhookEndpointId ep2 = new("crm-analytics");
        WebhookEndpointId ep3 = new("customer-webhook");
        WebhookEndpointId epUnrelated = new("inventory-service");

        // Sub 1: Exact match "order.created"
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep1, "order.created"));
        // Sub 2: Prefix wildcard "order.*"
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep2, "order.*"));
        // Sub 3: Universal wildcard "*"
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep3, "*"));
        // Sub Unrelated: Only listens to "payment.*" -> Must NOT receive order.created
        await store.SaveSubscriptionAsync(new WebhookSubscription(epUnrelated, "payment.*"));

        OrderCreatedEvent @event = new("ORD-100", 250.00m);

        // Act: 1 tekil event yayınlanır
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(@event);

        // Assert: 3 aboneye ayrı ayrı dispatch edilmeli
        Assert.Equal(3, handles.Count);
        Assert.Equal(3, dispatcher.Calls.Count);

        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep1);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep2);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == ep3);
        Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epUnrelated);
    }

    [Fact]
    public async Task PublishAsync_PassesExplicitPartitionKey_ToAllSubscribers() {
        // Arrange
        (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

        WebhookEndpointId ep1 = new("service-a");
        WebhookEndpointId ep2 = new("service-b");

        await store.SaveSubscriptionAsync(new WebhookSubscription(ep1, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep2, "order.*"));

        OrderCreatedEvent @event = new("ORD-555", 100m);
        const string customPartitionKey = "order-aggregate-555";

        // Act: Explicit partition key ile yayınlama
        await gateway.PublishAsync(@event, partitionKey: customPartitionKey);

        // Assert: Bütün abonelere AYNI custom partition key iletilmeli (Global FIFO sırası)
        Assert.Equal(2, dispatcher.Calls.Count);
        Assert.All(dispatcher.Calls, call => Assert.Equal(customPartitionKey, call.PartitionKey.Value));
    }

    [Fact]
    public async Task PublishAsync_DefaultsPartitionKeyToEndpointId_WhenNotExplicitlyProvided() {
        // Arrange
        (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

        WebhookEndpointId ep1 = new("tenant-x");
        WebhookEndpointId ep2 = new("tenant-y");

        await store.SaveSubscriptionAsync(new WebhookSubscription(ep1, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(ep2, "*"));

        OrderCreatedEvent @event = new("ORD-1", 50m);

        // Act: partitionKey verilmeden yayınlama
        await gateway.PublishAsync(@event);

        // Assert: Her abonenin partition key'i kendi EndpointId'sine eşit olmalı (İzole Shard sırası)
        Assert.Equal(2, dispatcher.Calls.Count);
        var call1 = dispatcher.Calls.First(c => c.EndpointId == ep1);
        var call2 = dispatcher.Calls.First(c => c.EndpointId == ep2);

        Assert.Equal(ep1.Value, call1.PartitionKey.Value);
        Assert.Equal(ep2.Value, call2.PartitionKey.Value);
    }

    [Fact]
    public async Task PublishAsync_SkipsDisabledSubscriptions() {
        // Arrange
        (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

        WebhookEndpointId activeEp = new("active-endpoint");
        WebhookEndpointId disabledEp = new("disabled-endpoint");

        await store.SaveSubscriptionAsync(new WebhookSubscription(activeEp, "order.*") { IsEnabled = true });
        await store.SaveSubscriptionAsync(new WebhookSubscription(disabledEp, "order.*") { IsEnabled = false });

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedEvent("ORD-1", 10m));

        // Assert: Sadece aktif aboneye gitmeli
        Assert.Single(handles);
        Assert.Single(dispatcher.Calls);
        Assert.Equal(activeEp, dispatcher.Calls[0].EndpointId);
    }

    [Fact]
    public async Task PublishAsync_ReturnsEmptyList_WhenZeroSubscribersMatch() {
        // Arrange: Mağazada sadece payment abonesi var ama order eventi atılıyor
        (WebhookPublisher gateway, InMemoryWebhookSubscriptionStore store, FakeWebhookDispatcher dispatcher) = CreateSut();

        await store.SaveSubscriptionAsync(new WebhookSubscription(new WebhookEndpointId("ep-payment"), "payment.*"));

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedEvent("ORD-1", 10m));

        // Assert
        Assert.Empty(handles);
        Assert.Empty(dispatcher.Calls);
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenPayloadIsNull() {
        (WebhookPublisher gateway, _, _) = CreateSut();

        await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
            gateway.PublishAsync<OrderCreatedEvent>(null!));
    }
}