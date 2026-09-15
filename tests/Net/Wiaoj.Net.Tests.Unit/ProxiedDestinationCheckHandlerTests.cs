using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>
/// Behind a proxy, destinations are checked from the request URL before they are sent; the check moved here from Webhooks
/// (#154).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "ProxiedDestinationCheck")]
public sealed class ProxiedDestinationCheckHandlerTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Stands in for the proxy: records what would have been sent to it.</summary>
    internal sealed class RecordingProxy : HttpMessageHandler {
        public List<Uri> Sent { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            lock(this.Sent) {
                this.Sent.Add(request.RequestUri!);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    internal static FakeDnsResolver Resolver() => new(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase) {
        ["partner.test"] = [IPAddress.Parse("8.8.8.8")],
        ["internal.test"] = [IPAddress.Parse("10.0.0.5"), IPAddress.Parse("169.254.169.254")],
        ["mixed.test"] = [IPAddress.Parse("10.0.0.5"), IPAddress.Parse("8.8.4.4")]
    });

    private static (HttpClient Client, RecordingProxy Proxy) Create(OutboundNetworkPolicy policy, DnsResolver resolver) {
        RecordingProxy proxy = new();
        return (new HttpClient(new ProxiedDestinationCheckHandler(policy, resolver) { InnerHandler = proxy }), proxy);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://127.0.0.1:6379/")]
    [InlineData("http://[::1]/hook")]
    [InlineData("http://[::ffff:10.0.0.5]/hook")]
    public async Task Should_Refuse_An_IP_Literal_Before_It_Reaches_The_Proxy(string url) {
        FakeDnsResolver resolver = Resolver();
        (HttpClient client, RecordingProxy proxy) = Create(OutboundNetworkPolicy.PublicOnly, resolver);
        using(client) {
            OutboundNetworkPolicyException refusal = await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync(url, Ct));

            Assert.Equal(OutboundRefusalReason.Address, refusal.Reason);
            Assert.Empty(proxy.Sent);
            Assert.Equal(0, resolver.Lookups);
        }
    }

    [Fact]
    public async Task Should_Refuse_A_Port_Without_Resolving_The_Host() {
        FakeDnsResolver resolver = Resolver();
        (HttpClient client, RecordingProxy proxy) = Create(OutboundNetworkPolicy.WebOnly, resolver);
        using(client) {
            OutboundNetworkPolicyException refusal = await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("https://partner.test:6379/", Ct));

            Assert.Equal(OutboundRefusalReason.Port, refusal.Reason);
            Assert.Equal(6379, refusal.Port);
            Assert.Empty(proxy.Sent);
            Assert.Equal(0, resolver.Lookups);
        }
    }

    [Fact]
    public async Task Should_Refuse_A_Name_That_Resolves_Only_To_Refused_Addresses_Without_Revealing_Them() {
        (HttpClient client, RecordingProxy proxy) = Create(OutboundNetworkPolicy.PublicOnly, Resolver());
        using(client) {
            OutboundNetworkPolicyException refusal = await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("https://internal.test/hook", Ct));

            Assert.Equal("internal.test", refusal.Host);
            Assert.DoesNotContain("10.0.0.5", refusal.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("169.254", refusal.Message, StringComparison.Ordinal);
            Assert.Empty(proxy.Sent);
        }
    }

    [Fact]
    public async Task Should_Report_A_Blocked_Network_As_Such() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { BlockedNetworks = [IPNetwork.Parse("10.0.0.0/8")] };
        (HttpClient client, _) = Create(policy, Resolver());
        using(client) {
            OutboundNetworkPolicyException refusal = await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("https://internal.test/", Ct));

            Assert.Equal(OutboundRefusalReason.BlockedNetwork, refusal.Reason);
        }
    }

    [Theory]
    [InlineData("https://partner.test/hook")]
    [InlineData("https://mixed.test/hook")]
    [InlineData("https://8.8.8.8/hook")]
    // A proxy is often there because this host has no DNS for the outside world; refusing here would break the request.
    [InlineData("https://unknown.test/hook")]
    public async Task Should_Send_A_Destination_It_Does_Not_Refuse_To_The_Proxy(string url) {
        (HttpClient client, RecordingProxy proxy) = Create(OutboundNetworkPolicy.PublicOnly, Resolver());
        using(client) {
            using HttpResponseMessage response = await client.GetAsync(url, Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal([new Uri(url)], proxy.Sent);
        }
    }

    [Fact]
    public async Task Should_Allow_A_Network_Named_In_The_Policy() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] };
        (HttpClient client, RecordingProxy proxy) = Create(policy, Resolver());
        using(client) {
            using HttpResponseMessage response = await client.GetAsync("http://10.20.5.1/hook", Ct);

            Assert.Single(proxy.Sent);
        }
    }

    [Fact]
    public void Should_Refuse_Missing_Arguments() {
        Assert.ThrowsAny<ArgumentNullException>(() => new ProxiedDestinationCheckHandler(null!));
        Assert.ThrowsAny<ArgumentNullException>(() => new ProxiedDestinationCheckHandler(OutboundNetworkPolicy.PublicOnly, null!));
        Assert.ThrowsAny<ArgumentNullException>(() => new ServiceCollection().AddHttpClient("x").AddProxiedDestinationCheck(null!));
    }

    public sealed class RegisteredOnAClient {
        [Fact]
        public async Task Should_Check_With_The_Registered_Resolver() {
            FakeDnsResolver resolver = Resolver();
            RecordingProxy proxy = new();
            ServiceCollection services = new();
            services.AddSingleton<DnsResolver>(resolver);
            services.AddHttpClient("proxied")
                .ConfigurePrimaryHttpMessageHandler(() => proxy)
                .AddProxiedDestinationCheck(OutboundNetworkPolicy.PublicOnly);

            await using ServiceProvider provider = services.BuildServiceProvider();
            HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("proxied");

            await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("https://internal.test/", Ct));
            using HttpResponseMessage response = await client.GetAsync("https://partner.test/", Ct);

            Assert.Equal([new Uri("https://partner.test/")], proxy.Sent);
            Assert.Equal(2, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Fall_Back_To_The_System_Resolver_For_IP_Literals_And_Ports() {
            RecordingProxy proxy = new();
            ServiceCollection services = new();
            services.AddHttpClient("proxied")
                .ConfigurePrimaryHttpMessageHandler(() => proxy)
                .AddProxiedDestinationCheck(OutboundNetworkPolicy.WebOnly);

            await using ServiceProvider provider = services.BuildServiceProvider();
            HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("proxied");

            await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("http://127.0.0.1/", Ct));
            await Assert.ThrowsAsync<OutboundNetworkPolicyException>(() => client.GetAsync("https://8.8.8.8:8443/", Ct));
            Assert.Empty(proxy.Sent);
        }
    }
}
