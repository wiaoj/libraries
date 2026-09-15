using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Net;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

/// <summary>A policy's port rules reach endpoint validation and the proxied destination check (#141).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "PortRules")]
public sealed class WebhookPortRulesTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("https://8.8.8.8/hook", true)]
    [InlineData("https://8.8.8.8:8443/hook", false)]
    public async Task Should_Refuse_An_Endpoint_On_A_Port_The_Policy_Does_Not_Allow(string url, bool built) {
        WebhookEndpointBuildResult result = await new WebhookEndpointBuilder()
            .WithId("ep_ports").WithTargetUrl(url).WithSecret("whsec_port_key_1234567890", new FakeSecretProtector<WebhookSigningContext>())
            .WithNetworkPolicy(OutboundNetworkPolicy.WebOnly).TryBuildAsync(Ct);

        Assert.Equal(built, result.IsSuccess);
        if(!built) {
            Assert.Equal(WebhookEndpointBuildStatus.DestinationRefused, result.Status);
            Assert.Contains("port 8443", result.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Should_Refuse_A_Proxied_Delivery_To_A_Port_The_Policy_Does_Not_Allow() {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddWiaojWebhooks()
            .UseNetworkPolicy(OutboundNetworkPolicy.WebOnly)
            .UseProxy("http://egress.example.com:3128")
            .ConfigureSecurity(o => o.ProxyEnforcesEgressPolicy = true);
        services.AddHttpClient<HttpWebhookSender>().ConfigurePrimaryHttpMessageHandler(() => new NeverCalledHandler());

        await using ServiceProvider provider = services.BuildServiceProvider();
        WebhookDeliveryResult result = await provider.GetRequiredService<IWebhookDeliverer>().DeliverAsync(
            WebhookTestFactory.CreateContext(endpoint: WebhookTestFactory.CreateEndpoint(new Uri("https://8.8.8.8:6379/hook"))), Ct);

        WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
        Assert.Equal(PermanentFailureReason.InvalidDestination, failure.Reason);
    }

    private sealed class NeverCalledHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            throw new InvalidOperationException("The refused destination must not reach the proxy.");
        }
    }
}