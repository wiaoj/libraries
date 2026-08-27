using Wiaoj.Abstractions;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

[Trait("Category", "Unit")]
[Trait("Feature", "Cloning")]
[Trait("Component", "DeliveryContext")]
public sealed class WebhookDeliveryContextCloningTests {

    [Fact]
    public void DeepClone_IsolatesItemsDictionary_PreventingStatePollution() {
        // Arrange
        WebhookDeliveryContext original = WebhookTestFactory.CreateContext();
        original.SetHeader("X-Original-Header", "Value1");
        original.Items["custom_key"] = "initial_state";

        // Act
        WebhookDeliveryContext clone = original.DeepClone();

        // Mutate the clone's dictionary
        clone.SetHeader("X-Clone-Header", "Value2");
        clone.Items["custom_key"] = "mutated_state";

        // Assert: Original context's items dictionary must remain completely untouched
        Assert.False(original.GetHeaders().ContainsKey("X-Clone-Header"));
        Assert.Equal("initial_state", original.Items["custom_key"]);

        Assert.True(clone.GetHeaders().ContainsKey("X-Clone-Header"));
        Assert.Equal("mutated_state", clone.Items["custom_key"]);
    }

    [Fact]
    public void DeepClone_IsolatesAttemptHistoryCollection() {
        // Arrange
        WebhookDeliveryAttempt attempt1 = WebhookTestFactory.CreateAttempt(1);
        WebhookDeliveryContext original = WebhookTestFactory.CreateContext(attemptHistory: [attempt1]);

        // Act
        WebhookDeliveryContext clone = original.DeepClone();

        // Assert: Same attempt data, but distinct list reference
        WebhookDeliveryAttempt item = Assert.Single(clone.AttemptHistory);
        Assert.Same(original.AttemptHistory[0], item);
        Assert.NotSame(original.AttemptHistory, clone.AttemptHistory);
    }

    [Fact]
    public void ExtensionMethod_Clone_InvokesDeepCloneByDefault() {
        WebhookDeliveryContext original = WebhookTestFactory.CreateContext();
        original.Items["key"] = "value";

        WebhookDeliveryContext clone = original.Clone(); // CloneableExtensions.Clone()

        clone.Items["key"] = "new_value";
        Assert.Equal("value", original.Items["key"]);
    }
}