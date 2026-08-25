using Wiaoj.Webhooks.Publishing.Internal;
using Wiaoj.Webhooks.Publishing.Tests.Unit.TestData;
using Xunit;

namespace Wiaoj.Webhooks.Publishing.Tests.Unit.Matching;

[Trait("Category", "Unit")]
[Trait("Feature", "Publishing")]
[Trait("Component", "CompositeMatcher")]
public sealed class CompositeSubscriptionMatcherTests {
    private readonly CompositeSubscriptionMatcher _matcher = new(new WildcardTopicMatcher(), new SimpleContentFilterEvaluator());

    [Fact]
    public void Matches_ReturnsTrue_WhenBothTopicAndContentFilterMatch() {
        WebhookSubscription subscription = new(new WebhookEndpointId("ep-1"), "order.*") {
            FilterExpression = "Amount >= 100"
        };
        OrderCreatedWebhookEvent payload = new("ORD-1", 150m);

        bool result = this._matcher.Matches(subscription, "order.created", payload);
        Assert.True(result);
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenTopicMatches_ButContentFilterFails() {
        WebhookSubscription subscription = new(new WebhookEndpointId("ep-1"), "order.*") {
            FilterExpression = "Amount >= 500"
        };
        OrderCreatedWebhookEvent payload = new("ORD-1", 150m);

        bool result = this._matcher.Matches(subscription, "order.created", payload);
        Assert.False(result);
    }

    [Fact]
    public void Matches_ReturnsFalse_WhenContentFilterMatches_ButTopicFails() {
        WebhookSubscription subscription = new(new WebhookEndpointId("ep-1"), "payment.*") {
            FilterExpression = "Amount >= 100"
        };
        OrderCreatedWebhookEvent payload = new("ORD-1", 150m);

        bool result = this._matcher.Matches(subscription, "order.created", payload);
        Assert.False(result);
    }

    [Fact]
    public void Matches_ReturnsTrue_WhenNoFilterExpressionProvided() {
        WebhookSubscription subscription = new(new WebhookEndpointId("ep-1"), "order.*") {
            FilterExpression = null
        };
        OrderCreatedWebhookEvent payload = new("ORD-1", 150m);

        bool result = this._matcher.Matches(subscription, "order.created", payload);
        Assert.True(result);
    }
}