using Wiaoj.Security;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

public sealed class WebhookEndpointTests {
    [Fact]
    public void Constructor_SetsAllProperties_WhenValid() {
        WebhookEndpointId id = WebhookTestFactory.CreateEndpointId();
        Uri targetUrl = WebhookTestFactory.CreateTargetUrl();
        EncryptedSecret<WebhookSigningContext> secret = WebhookTestFactory.CreateEncryptedSecret();

        WebhookEndpoint endpoint = new(id, targetUrl, secret);

        Assert.Equal(id, endpoint.Id);
        Assert.Equal(targetUrl, endpoint.TargetUrl);
        Assert.Equal(secret, endpoint.Secret);
    }

    [Fact]
    public void Constructor_ThrowsWhenTargetUrlIsNull() {
        Assert.ThrowsAny<ArgumentNullException>(() =>
            new WebhookEndpoint(
                WebhookTestFactory.CreateEndpointId(),
                null!,
                WebhookTestFactory.CreateEncryptedSecret()));
    }
}