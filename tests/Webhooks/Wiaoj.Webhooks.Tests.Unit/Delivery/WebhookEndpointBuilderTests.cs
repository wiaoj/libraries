using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Signing;

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
        WebhookEndpoint endpoint = await builder.BuildAsync(TestContext.Current.CancellationToken);

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
        await Assert.ThrowsAsync<WebhookSsrfBlockedException>(() => builder.BuildAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BuildAsync_ThrowsInvalidOperationException_WhenRequiredFieldsMissing() {
        WebhookEndpointBuilder builder = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.BuildAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BuildAsync_WithCustomSignerAndHeaders_BuildsEndpointCorrectly() {
        // Arrange
        HmacSha512WebhookSigner customSigner = new("X-Custom-Sign");
        WebhookEndpointBuilder builder = new();
        builder.WithId("ep_enterprise_1")
               .WithTargetUrl("https://bank.com/webhooks")
               .WithSecret("whsec_secure_key_1234567890", this._protector)
               .WithSigner(customSigner)
               .WithHeader("Authorization", "Bearer static_token_123")
               .WithHeader("X-Tenant-Id", "tenant_99")
               .WithSsrfValidation(validate: false, allowPrivateNetworks: false);

        // Act
        WebhookEndpoint endpoint = await builder.BuildAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(customSigner, endpoint.CustomSigner);
        Assert.NotNull(endpoint.CustomHeaders);
        Assert.Equal(2, endpoint.CustomHeaders.Count);
        Assert.Equal("Bearer static_token_123", endpoint.CustomHeaders["Authorization"]);
        Assert.Equal("tenant_99", endpoint.CustomHeaders["X-Tenant-Id"]);
    }
}