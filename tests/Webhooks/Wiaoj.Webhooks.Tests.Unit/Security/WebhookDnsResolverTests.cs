using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Sockets;
using Wiaoj.Net;
using Wiaoj.Security.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Security;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

/// <summary>Deliveries, the proxied destination check and endpoint validation resolve names with the configured DnsResolver (#139).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "DnsResolver")]
public sealed class WebhookDnsResolverTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class TableResolver(Dictionary<string, IPAddress[]> records) : DnsResolver {
        public int Lookups;

        public override ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) {
            Interlocked.Increment(ref this.Lookups);
            return records.TryGetValue(host, out IPAddress[]? addresses)
                ? ValueTask.FromResult(addresses)
                : ValueTask.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound));
        }
    }

    private static TableResolver Resolver() => new(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase) {
        ["partner.test"] = [IPAddress.Parse("8.8.8.8")],
        ["metadata.test"] = [IPAddress.Parse("169.254.169.254")]
    });

    [Fact]
    public async Task Should_Refuse_A_Delivery_To_A_Name_The_Registered_Resolver_Maps_To_A_Refused_Address() {
        TableResolver resolver = Resolver();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<DnsResolver>(resolver);
        services.AddWiaojWebhooks();

        await using ServiceProvider provider = services.BuildServiceProvider();
        IWebhookDeliverer deliverer = provider.GetRequiredService<IWebhookDeliverer>();

        WebhookDeliveryResult result = await deliverer.DeliverAsync(
            WebhookTestFactory.CreateContext(endpoint: WebhookTestFactory.CreateEndpoint(new Uri("http://metadata.test/latest"))), Ct);

        WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
        Assert.Equal(PermanentFailureReason.InvalidDestination, failure.Reason);
        Assert.True(resolver.Lookups > 0);
    }

    [Fact]
    public async Task Should_Use_The_Registered_Resolver_For_The_Proxied_Destination_Check() {
        TableResolver resolver = Resolver();
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<DnsResolver>(resolver);
        services.AddWiaojWebhooks()
            .UseProxy("http://egress.example.com:3128")
            .ConfigureSecurity(o => o.ProxyEnforcesEgressPolicy = true);
        services.AddHttpClient<HttpWebhookSender>().ConfigurePrimaryHttpMessageHandler(() => new NeverCalledHandler());

        await using ServiceProvider provider = services.BuildServiceProvider();
        WebhookDeliveryResult result = await provider.GetRequiredService<IWebhookDeliverer>().DeliverAsync(
            WebhookTestFactory.CreateContext(endpoint: WebhookTestFactory.CreateEndpoint(new Uri("http://metadata.test/latest"))), Ct);

        Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
        Assert.Equal(1, resolver.Lookups);
    }

    [Fact]
    public async Task Should_Validate_An_Endpoint_With_The_Builder_Resolver() {
        FakeSecretProtector<WebhookSigningContext> protector = new();
        TableResolver resolver = Resolver();

        WebhookEndpointBuildResult refused = await new WebhookEndpointBuilder()
            .WithId("ep_dns").WithTargetUrl("https://metadata.test/hook").WithSecret("whsec_dns_key_1234567890", protector)
            .WithDnsResolver(resolver).TryBuildAsync(Ct);
        WebhookEndpointBuildResult built = await new WebhookEndpointBuilder()
            .WithId("ep_dns").WithTargetUrl("https://partner.test/hook").WithSecret("whsec_dns_key_1234567890", protector)
            .WithDnsResolver(resolver).TryBuildAsync(Ct);

        Assert.Equal(WebhookEndpointBuildStatus.DestinationRefused, refused.Status);
        Assert.True(built.IsSuccess);
        Assert.Equal(2, resolver.Lookups);
    }

    private sealed class NeverCalledHandler : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            throw new InvalidOperationException("The refused destination must not reach the proxy.");
        }
    }
}
