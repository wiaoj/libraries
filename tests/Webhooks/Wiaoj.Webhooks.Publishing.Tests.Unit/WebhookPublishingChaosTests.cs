using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "Chaos")]
public sealed class WebhookPublisherChaosTests {

    [Fact]
    public async Task PublishAsync_WhenSameEndpointHasMultipleMatchingSubscriptions_DeduplicatesEndpointDispatches() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId sameEndpoint = new("crm-service");
        await store.SaveSubscriptionAsync(new WebhookSubscription(sameEndpoint, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(sameEndpoint, "order.created"));

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-1", 50m));

        // Assert: Endpoint must receive only one dispatch call despite matching multiple rules
        Assert.Single(dispatcher.Calls);
        Assert.Single(handles);
    }
}