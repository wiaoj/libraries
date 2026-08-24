using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit;

[Trait("Category", "Unit")]
[Trait("Feature", "Gateway")]
[Trait("Component", "FanOutTopology")]
public sealed class WebhookPublisherFanOutTopologyTests {

    [Fact]
    public async Task PublishAsync_WithComplexTopology_DispatchesOnlyToMatchingAndActiveEndpoints() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(store: store, dispatcher: dispatcher);

        WebhookEndpointId epExactMatch = new("ep-exact");
        WebhookEndpointId epPrefixMatch = new("ep-prefix");
        WebhookEndpointId epSuffixMatch = new("ep-suffix");
        WebhookEndpointId epUniversalMatch = new("ep-universal");
        WebhookEndpointId epDisabledMatch = new("ep-disabled");
        WebhookEndpointId epUnmatched = new("ep-unmatched");

        // Subscriptions matrix
        await store.SaveSubscriptionAsync(new WebhookSubscription(epExactMatch, "order.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(epPrefixMatch, "order.*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(epSuffixMatch, "*.created"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(epUniversalMatch, "*"));
        await store.SaveSubscriptionAsync(new WebhookSubscription(epDisabledMatch, "order.created") { IsEnabled = false });
        await store.SaveSubscriptionAsync(new WebhookSubscription(epUnmatched, "payment.*"));

        // Act: Publish single event
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(new OrderCreatedWebhookEvent("ORD-COMPLEX", 250m));

        // Assert: Exactly 4 distinct active subscribers must receive the event
        Assert.Equal(4, handles.Count);
        Assert.Equal(4, dispatcher.Calls.Count);

        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epExactMatch);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epPrefixMatch);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epSuffixMatch);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epUniversalMatch);

        Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epDisabledMatch);
        Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epUnmatched);
    }
}