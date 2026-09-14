using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Sockets;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Host names are resolved by the DnsResolver given or registered, never behind its back (#139).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "DnsResolver")]
public sealed class DnsResolverTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly OutboundNetworkPolicy LoopbackAllowed = OutboundNetworkPolicy.PublicOnly with {
        AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")]
    };

    // .test is reserved never to exist in real DNS (RFC 6761): a connection to these names proves the fake was used.
    private static FakeDnsResolver Resolver() => new(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase) {
        ["partner.test"] = [IPAddress.Parse("8.8.8.8")],
        ["internal.test"] = [IPAddress.Parse("10.0.0.5")],
        ["loopback.test"] = [IPAddress.Loopback],
        ["mixed.test"] = [IPAddress.Parse("169.254.169.254"), IPAddress.Parse("8.8.4.4")]
    });

    public sealed class CheckingAHost {
        [Theory]
        [InlineData("partner.test", OutboundHostStatus.Allowed)]
        [InlineData("internal.test", OutboundHostStatus.Refused)]
        [InlineData("mixed.test", OutboundHostStatus.Allowed)]
        public async Task Should_Decide_By_The_Addresses_The_Resolver_Returns(string host, OutboundHostStatus status) {
            FakeDnsResolver resolver = Resolver();

            OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(host, resolver, Ct);

            Assert.Equal(status, check.Status);
            Assert.Equal(1, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Report_A_Name_The_Resolver_Cannot_Resolve_As_Unresolvable() {
            OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync("unknown.test", Resolver(), Ct);

            Assert.Equal(OutboundHostStatus.Unresolvable, check.Status);
            Assert.IsType<SocketException>(check.ResolutionError);
        }

        [Theory]
        [InlineData("8.8.8.8")]
        [InlineData("[::1]")]
        [InlineData("169.254.169.254")]
        public async Task Should_Never_Send_An_IP_Literal_To_The_Resolver(string literal) {
            FakeDnsResolver resolver = Resolver();

            await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(literal, resolver, Ct);

            Assert.Equal(0, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Use_The_Resolver_For_A_Url() {
            FakeDnsResolver resolver = Resolver();

            OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(new Uri("https://internal.test/hook"), resolver, Ct);

            Assert.Equal(OutboundHostStatus.Refused, check.Status);
            Assert.Equal(1, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Let_An_Unexpected_Resolver_Failure_Propagate() {
            // Only "cannot resolve" (SocketException) is an outcome; a broken resolver is a fault, not an answer.
            DnsResolver broken = new ThrowingResolver();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await OutboundNetworkPolicy.PublicOnly.CheckHostAsync("partner.test", broken, Ct));
        }

        [Fact]
        public async Task Should_Resolve_With_The_Operating_System_By_Default() {
            IPAddress[] addresses = await DnsResolver.System.ResolveAsync("localhost", Ct);

            Assert.Contains(addresses, IPAddress.IsLoopback);
        }

        private sealed class ThrowingResolver : DnsResolver {
            public override ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) {
                return ValueTask.FromException<IPAddress[]>(new InvalidOperationException("resolver misconfigured"));
            }
        }
    }

    public sealed class Connecting {
        [Fact]
        public async Task Should_Connect_Through_The_Address_The_Resolver_Returns() {
            using LoopbackHttpServer server = new();
            FakeDnsResolver resolver = Resolver();
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(LoopbackAllowed, resolver));

            using HttpResponseMessage response = await client.GetAsync($"http://loopback.test:{server.Port}/", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Refuse_A_Name_The_Resolver_Maps_To_A_Refused_Address() {
            using LoopbackHttpServer server = new();
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly, Resolver()));

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://loopback.test:{server.Port}/", Ct));

            Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
            Assert.Equal(0, server.Connections);
        }

        [Fact]
        public async Task Should_Use_The_Resolver_Registered_In_The_Container() {
            using LoopbackHttpServer server = new();
            FakeDnsResolver resolver = Resolver();
            ServiceCollection services = new();
            services.AddSingleton<DnsResolver>(resolver);
            services.AddHttpClient("partner").AddOutboundNetworkPolicy(LoopbackAllowed);

            await using ServiceProvider provider = services.BuildServiceProvider();
            using HttpClient client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("partner");
            using HttpResponseMessage response = await client.GetAsync($"http://loopback.test:{server.Port}/", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, resolver.Lookups);
        }

        [Fact]
        public void Should_Refuse_A_Null_Resolver() {
            Assert.ThrowsAny<ArgumentNullException>(() => new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly, null!));
        }
    }
}
