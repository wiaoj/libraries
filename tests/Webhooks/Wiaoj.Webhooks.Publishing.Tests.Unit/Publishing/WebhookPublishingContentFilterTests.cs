using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Publishing;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "ContentFilterFanOut")]
public sealed class WebhookPublishingContentFilterTests {

    [Fact]
    public async Task PublishAsync_WithContentFilterExpression_DispatchesOnlyToMatchingSubscribers() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        SimpleContentFilterEvaluator contentEvaluator = new();
        CompositeSubscriptionMatcher matcher = new(contentEvaluator);

        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(
            store: store,
            matcher: matcher,
            dispatcher: dispatcher);

        WebhookEndpointId epVipOnly = new("ep-vip-accounting");
        WebhookEndpointId epHighValue = new("ep-high-value-fraud");
        WebhookEndpointId epAllOrders = new("ep-all-orders-data-lake");

        // Subscriptions on same topic "order.*" with different content filters
        await store.SaveSubscriptionAsync(new WebhookSubscription(epVipOnly, "order.*") {
            FilterExpression = "Amount >= 1000"
        }, TestContext.Current.CancellationToken);
        await store.SaveSubscriptionAsync(new WebhookSubscription(epHighValue, "order.*") {
            FilterExpression = "Amount >= 500 && Amount < 1000"
        }, TestContext.Current.CancellationToken);
        await store.SaveSubscriptionAsync(new WebhookSubscription(epAllOrders, "order.*") {
            FilterExpression = null // Listens to all
        }, TestContext.Current.CancellationToken);

        OrderCreatedWebhookEvent order750 = new("ORD-750", 750m);

        // Act: Publish an order with Amount = 750
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(
            order750,
            TestContext.Current.CancellationToken);

        // Assert: 
        // - epHighValue must receive (750 in [500..1000])
        // - epAllOrders must receive (no filter)
        // - epVipOnly must NOT receive (750 < 1000)
        Assert.Equal(2, handles.Count);
        Assert.Equal(2, dispatcher.Calls.Count);

        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epHighValue);
        Assert.Contains(dispatcher.Calls, c => c.EndpointId == epAllOrders);
        Assert.DoesNotContain(dispatcher.Calls, c => c.EndpointId == epVipOnly);
    }
}