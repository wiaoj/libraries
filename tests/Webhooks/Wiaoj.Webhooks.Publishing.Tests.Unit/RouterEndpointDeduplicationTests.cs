using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Internal;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "Deduplication")]
public sealed class RouterEndpointDeduplicationTests {

    [Fact]
    public async Task PublishAsync_WhenMultipleRulesMatchSameEndpoint_ExecutesSingleDispatch() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new(); 
        FakeWebhookDispatcher dispatcher = new();  
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId sameEndpoint = new("analytics-service");
        await store.SaveSubscriptionAsync(new WebhookSubscription(sameEndpoint, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(sameEndpoint, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(sameEndpoint, "order.created"));

        // Act
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-99", 100m));

        // Assert: Universal wildcard, prefix wildcard, and exact match must yield exactly one dispatch
        Assert.Single(dispatcher.Calls);
        Assert.Single(handles);
        Assert.Equal(sameEndpoint, dispatcher.Calls[0].EndpointId);
    }
}