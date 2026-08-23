using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Exceptions;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

[Trait("Category", "Unit")]
[Trait("Feature", "Builder")]
[Trait("Component", "WebhookEndpoint")]
public sealed class WebhookEndpointBuilderTests {
    private readonly FakeSecretProtector<WebhookSigningContext> _protector = new();

    [Fact]
    public async Task BuildAsync_SuccessfullyConstructsEndpoint_WhenConfigurationIsValid() {
        // Arrange
        WebhookEndpointBuilder builder = new();
        builder.WithId("ep_test_100")
               .WithTargetUrl("https://example.com/webhook")
               .WithSecret("whsec_secure_key_1234567890", this._protector)
               .WithSsrfValidation(validate: false);

        // Act
        WebhookEndpoint endpoint = await builder.BuildAsync();

        // Assert
        Assert.Equal("ep_test_100", endpoint.Id.Value);
        Assert.Equal(new Uri("https://example.com/webhook"), endpoint.TargetUrl);
        Assert.NotEqual(default, endpoint.Secret);
    }

    [Fact]
    public async Task BuildAsync_BlocksLoopbackAddresses_WhenSsrfValidationIsEnabled() {
        // Arrange
        WebhookEndpointBuilder builder = new();
        builder.WithId("ep_blocked")
               .WithTargetUrl("http://127.0.0.1/hook")
               .WithSecret("whsec_key", this._protector)
               .WithSsrfValidation(validate: true, allowPrivateNetworks: false);

        // Act & Assert
        await Assert.ThrowsAsync<WebhookSsrfBlockedException>(() => builder.BuildAsync());
    }

    [Fact]
    public async Task BuildAsync_ThrowsInvalidOperationException_WhenRequiredFieldsMissing() {
        WebhookEndpointBuilder builder = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync());
    }
}