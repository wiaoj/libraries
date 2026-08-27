using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

public sealed class WebhookDeliveryContextTests {
    [Fact]
    public void TargetUrl_ReturnsEndpointTargetUrl() {
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(new Uri("https://example.com/hook"));
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

        Assert.Equal(new Uri("https://example.com/hook"), context.TargetUrl);
    }

    [Fact]
    public void Items_IsEmptyByDefault() {
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        Assert.Empty(context.Items);
    }

    [Fact]
    public void Items_AllowsMiddlewareToShareStateAcrossPipelineSteps() {
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

        context.Items["signature"] = "sha256=abc123";

        Assert.Equal("sha256=abc123", context.Items["signature"]);
    }

    [Fact]
    public void AttemptHistory_DefaultsToProvidedList_WithoutMutatingCaller() {
        List<WebhookDeliveryAttempt> history = [WebhookTestFactory.CreateAttempt(attemptNumber: 1)];
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(attemptHistory: history);

        WebhookDeliveryAttempt item = Assert.Single(context.AttemptHistory);
        Assert.Same(history[0], item);
    }
}