using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.Fakes;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Publishing;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "FanOutDeduplication")]
public sealed class PublishingContentFilterDeduplicationTests {

    [Fact]
    public async Task PublishAsync_WhenSameEndpointMatchesMultipleRules_DispatchesExactlyOnce() {
        // Arrange
        InMemoryWebhookSubscriptionStore store = new();
        FakeWebhookDispatcher dispatcher = new();
        SimpleContentFilterEvaluator contentEvaluator = new();
        CompositeSubscriptionMatcher matcher = new(new WildcardTopicMatcher(), contentEvaluator);

        WebhookPublisher gateway = GatewayTestFactory.CreateGateway(
            store: store,
            matcher: matcher,
            dispatcher: dispatcher);

        WebhookEndpointId singleEndpoint = new("ep-multi-matching-subscriber");

        // Same endpoint has 3 subscriptions matching the same event:
        // Rule 1: Universal wildcard (*) with no filter
        await store.SaveSubscriptionAsync(new WebhookSubscription(singleEndpoint, "*"), TestContext.Current.CancellationToken);
        // Rule 2: Prefix wildcard (order.*) with amount filter (matching)
        await store.SaveSubscriptionAsync(new WebhookSubscription(singleEndpoint, "order.*") {
            FilterExpression = "Amount >= 50"
        }, TestContext.Current.CancellationToken);
        // Rule 3: Exact match (order.created) with currency filter (matching)
        await store.SaveSubscriptionAsync(new WebhookSubscription(singleEndpoint, "order.created") {
            FilterExpression = "Amount == 100"
        }, TestContext.Current.CancellationToken);

        OrderCreatedWebhookEvent order = new("ORD-DEDUP", 100m);

        // Act: Publish single event
        IReadOnlyList<WebhookDeliveryHandle> handles = await gateway.PublishAsync(
            order,
            TestContext.Current.CancellationToken);

        // Assert: 3 rules match, but endpoint must receive EXACTLY ONE dispatch!
        Assert.Single(handles);
        Assert.Single(dispatcher.Calls);
        Assert.Equal(singleEndpoint, dispatcher.Calls[0].EndpointId);
    }
}