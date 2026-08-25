using System.Text;
using Wiaoj.Webhooks.AspNetCore;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "PolicyPayloadPath")]
public sealed class WebhookReceiverPolicyPayloadPathTests {

    [Fact]
    public void PayloadPath_WhenSet_PreTokenizesUtf8SegmentsCorrectly() {
        WebhookReceiverPolicy policy = new() {
            Name = "Stripe"
        };

        policy.PayloadPath = "data.object.nested";

        Assert.Equal("data.object.nested", policy.PayloadPath);
        Assert.NotNull(policy.PayloadPathSegmentsUtf8);
        Assert.Equal(3, policy.PayloadPathSegmentsUtf8.Length);

        Assert.Equal("data", Encoding.UTF8.GetString(policy.PayloadPathSegmentsUtf8[0]));
        Assert.Equal("object", Encoding.UTF8.GetString(policy.PayloadPathSegmentsUtf8[1]));
        Assert.Equal("nested", Encoding.UTF8.GetString(policy.PayloadPathSegmentsUtf8[2]));
    }

    [Fact]
    public void PayloadPath_WhenSetToNullOrWhitespace_ClearsTokenizedSegments() {
        WebhookReceiverPolicy policy = new() {
            Name = "Test"
        };

        policy.PayloadPath = "data.object";
        Assert.NotNull(policy.PayloadPathSegmentsUtf8);

        policy.PayloadPath = null;
        Assert.Null(policy.PayloadPath);
        Assert.Null(policy.PayloadPathSegmentsUtf8);

        policy.PayloadPath = "   ";
        Assert.Equal("   ", policy.PayloadPath);
        Assert.Null(policy.PayloadPathSegmentsUtf8);
    }

    [Fact]
    public void WithPayloadPath_SetsPropertyAndEnablesFluentChaining() {
        WebhookReceiverPolicy policy = new WebhookReceiverPolicy()
            .WithPayloadPath("envelope.item");

        Assert.Equal("envelope.item", policy.PayloadPath);
        Assert.NotNull(policy.PayloadPathSegmentsUtf8);
        Assert.Equal(2, policy.PayloadPathSegmentsUtf8.Length);
    }
}