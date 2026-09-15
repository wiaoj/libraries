using System.Net;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Ports are part of the policy: blocked ports win, allowed ports restrict, WebOnly allows 80 and 443 (#141).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "PortRules")]
public sealed class PortRulesTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static readonly IPAddress PublicAddress = IPAddress.Parse("8.8.8.8");

    public sealed class Presets {
        [Fact]
        public void Should_Allow_Only_Public_Addresses_On_80_And_443_With_WebOnly() {
            OutboundNetworkPolicy web = OutboundNetworkPolicy.WebOnly;

            Assert.Equal(new HashSet<int> { 80, 443 }, web.AllowedPorts);
            Assert.Empty(web.BlockedPorts);
            Assert.Equal(new HashSet<IPAddressScope> { IPAddressScope.Public }, web.AllowedScopes);
            Assert.Empty(web.AllowedNetworks);
            Assert.Empty(web.BlockedNetworks);
        }

        [Fact]
        public void Should_Leave_PublicOnly_And_Unrestricted_Open_To_Any_Port() {
            Assert.Null(OutboundNetworkPolicy.PublicOnly.AllowedPorts);
            Assert.Empty(OutboundNetworkPolicy.PublicOnly.BlockedPorts);
            Assert.Null(OutboundNetworkPolicy.Unrestricted.AllowedPorts);
            Assert.Empty(OutboundNetworkPolicy.Unrestricted.BlockedPorts);

            Assert.True(OutboundNetworkPolicy.PublicOnly.IsAllowed(PublicAddress, 8443));
            Assert.True(OutboundNetworkPolicy.Unrestricted.IsAllowed(IPAddress.Loopback, 6379));
        }

        [Theory]
        [InlineData("8.8.8.8", 443, true)]
        [InlineData("8.8.8.8", 80, true)]
        [InlineData("2606:4700:4700::1111", 443, true)]
        [InlineData("8.8.8.8", 8443, false)]
        [InlineData("8.8.8.8", 22, false)]
        [InlineData("8.8.8.8", 6379, false)]
        [InlineData("10.0.0.1", 443, false)]
        [InlineData("169.254.169.254", 80, false)]
        public void Should_Decide_Address_And_Port_Together_With_WebOnly(string address, int port, bool allowed) {
            Assert.Equal(allowed, OutboundNetworkPolicy.WebOnly.IsAllowed(IPAddress.Parse(address), port));
        }
    }

    public sealed class Rules {
        [Fact]
        public void Should_Refuse_A_Blocked_Port_Even_When_Allowed_Ports_Contain_It() {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with {
                AllowedPorts = new HashSet<int> { 443, 8443 },
                BlockedPorts = new HashSet<int> { 8443 }
            };

            Assert.True(policy.IsAllowed(PublicAddress, 443));
            Assert.False(policy.IsAllowed(PublicAddress, 8443));
        }

        [Fact]
        public void Should_Refuse_Only_Blocked_Ports_When_No_Allowed_Ports_Are_Set() {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { BlockedPorts = new HashSet<int> { 6379, 2375 } };

            Assert.False(policy.IsAllowed(PublicAddress, 6379));
            Assert.False(policy.IsAllowed(PublicAddress, 2375));
            Assert.True(policy.IsAllowed(PublicAddress, 8443));
        }

        [Fact]
        public void Should_Still_Refuse_A_Refused_Address_On_An_Allowed_Port() {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedPorts = new HashSet<int> { 443 } };

            Assert.False(policy.IsAllowed(IPAddress.Loopback, 443));
        }

        [Fact]
        public void Should_Keep_The_Address_Only_Overload_Independent_Of_Ports() {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.WebOnly with { BlockedPorts = new HashSet<int> { 443 } };

            Assert.True(policy.IsAllowed(PublicAddress));
            Assert.False(policy.IsAllowed(PublicAddress, 443));
        }

        [Fact]
        public void Should_Not_Change_When_The_Set_It_Was_Given_Changes() {
            HashSet<int> allowed = [443];
            HashSet<int> blocked = [22];
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedPorts = allowed, BlockedPorts = blocked };
            allowed.Add(8443);
            blocked.Add(443);

            Assert.False(policy.IsAllowed(PublicAddress, 8443));
            Assert.True(policy.IsAllowed(PublicAddress, 443));
        }

        [Fact]
        public void Should_Allow_Any_Port_Again_When_Allowed_Ports_Are_Reset_To_Null() {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.WebOnly with { AllowedPorts = null };

            Assert.True(policy.IsAllowed(PublicAddress, 8443));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void Should_Refuse_A_Port_Outside_The_Valid_Range(int port) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => OutboundNetworkPolicy.PublicOnly with { AllowedPorts = new HashSet<int> { 443, port } });
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => OutboundNetworkPolicy.PublicOnly with { BlockedPorts = new HashSet<int> { port } });
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => OutboundNetworkPolicy.PublicOnly.IsAllowed(PublicAddress, port));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(65535)]
        public void Should_Accept_The_Bounds_Of_The_Valid_Range(int port) {
            OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedPorts = new HashSet<int> { port } };

            Assert.True(policy.IsAllowed(PublicAddress, port));
            Assert.ThrowsAny<ArgumentNullException>(() => OutboundNetworkPolicy.PublicOnly with { BlockedPorts = null! });
        }
    }

    public sealed class CheckingAUrl {
        private static FakeDnsResolver Resolver() => new(new Dictionary<string, IPAddress[]>(StringComparer.OrdinalIgnoreCase) {
            ["partner.test"] = [PublicAddress],
            ["internal.test"] = [IPAddress.Parse("10.0.0.5")]
        });

        [Theory]
        // An explicit port, or the scheme's default.
        [InlineData("https://partner.test/hook", OutboundHostStatus.Allowed)]
        [InlineData("http://partner.test/hook", OutboundHostStatus.Allowed)]
        [InlineData("https://partner.test:443/hook", OutboundHostStatus.Allowed)]
        [InlineData("https://partner.test:8443/hook", OutboundHostStatus.Refused)]
        [InlineData("http://partner.test:6379/", OutboundHostStatus.Refused)]
        [InlineData("ftp://partner.test/", OutboundHostStatus.Refused)]
        public async Task Should_Check_The_Port_The_Url_Will_Use(string url, OutboundHostStatus status) {
            OutboundHostCheck check = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri(url), Resolver(), Ct);

            Assert.Equal(status, check.Status);
        }

        [Fact]
        public async Task Should_Refuse_A_Port_Without_Resolving_The_Host() {
            FakeDnsResolver resolver = Resolver();

            OutboundHostCheck check = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("https://unknown.test:8443/"), resolver, Ct);

            Assert.Equal(OutboundHostStatus.Refused, check.Status);
            Assert.Equal(OutboundRefusalReason.Port, check.RefusalReason);
            Assert.Equal(0, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Report_A_Refused_Address_As_Such() {
            OutboundHostCheck check = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("https://internal.test/"), Resolver(), Ct);

            Assert.Equal(OutboundHostStatus.Refused, check.Status);
            Assert.Equal(OutboundRefusalReason.Address, check.RefusalReason);
        }

        [Fact]
        public async Task Should_Report_No_Refusal_Reason_Unless_Refused() {
            OutboundHostCheck allowed = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("https://partner.test/"), Resolver(), Ct);
            OutboundHostCheck unresolvable = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(new Uri("https://unknown.test/"), Resolver(), Ct);

            Assert.Null(allowed.RefusalReason);
            Assert.Equal(OutboundHostStatus.Unresolvable, unresolvable.Status);
            Assert.Null(unresolvable.RefusalReason);
        }

        [Fact]
        public async Task Should_Refuse_A_Url_Without_A_Known_Port_Only_When_Ports_Are_Restricted() {
            // A scheme with no default port and no explicit port: the port it will use is unknown.
            Uri url = new("custom://partner.test/");
            Assert.Equal(-1, url.Port);

            OutboundHostCheck restricted = await OutboundNetworkPolicy.WebOnly.CheckHostAsync(url, Resolver(), Ct);
            OutboundHostCheck blockedOnly = await (OutboundNetworkPolicy.PublicOnly with { BlockedPorts = new HashSet<int> { 22 } }).CheckHostAsync(url, Resolver(), Ct);

            Assert.Equal(OutboundHostStatus.Refused, restricted.Status);
            Assert.Equal(OutboundRefusalReason.Port, restricted.RefusalReason);
            Assert.Equal(OutboundHostStatus.Allowed, blockedOnly.Status);
        }
    }

    public sealed class Connecting {
        private static OutboundNetworkPolicy LoopbackAllowed(Func<OutboundNetworkPolicy, OutboundNetworkPolicy> ports) =>
            ports(OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")] });

        [Fact]
        public async Task Should_Refuse_A_Connection_To_A_Port_Not_Allowed_Without_Connecting_Or_Resolving() {
            using LoopbackHttpServer server = new();
            FakeDnsResolver resolver = new(new Dictionary<string, IPAddress[]> { ["loopback.test"] = [IPAddress.Loopback] });
            OutboundNetworkPolicy policy = LoopbackAllowed(p => p with { AllowedPorts = new HashSet<int> { 443 } });
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(policy, resolver));

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://loopback.test:{server.Port}/", Ct));

            OutboundNetworkPolicyException refusal = Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
            Assert.Equal(OutboundRefusalReason.Port, refusal.Reason);
            Assert.Equal(server.Port, refusal.Port);
            Assert.Contains($"port {server.Port} is not allowed", refusal.Message, StringComparison.Ordinal);
            Assert.Equal(0, server.Connections);
            Assert.Equal(0, resolver.Lookups);
        }

        [Fact]
        public async Task Should_Refuse_A_Blocked_Port() {
            using LoopbackHttpServer server = new();
            OutboundNetworkPolicy policy = LoopbackAllowed(p => p with { BlockedPorts = new HashSet<int> { server.Port } });
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(policy));

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct));

            Assert.Equal(OutboundRefusalReason.Port, Assert.IsType<OutboundNetworkPolicyException>(error.InnerException).Reason);
            Assert.Equal(0, server.Connections);
        }

        [Fact]
        public async Task Should_Connect_On_An_Allowed_Port() {
            using LoopbackHttpServer server = new();
            OutboundNetworkPolicy policy = LoopbackAllowed(p => p with { AllowedPorts = new HashSet<int> { 443, server.Port } });
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(policy));

            using HttpResponseMessage response = await client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(1, server.Connections);
        }

        [Fact]
        public async Task Should_Report_An_Address_Refusal_With_Its_Reason() {
            using LoopbackHttpServer server = new();
            using HttpClient client = new(new SocketsHttpHandler().UseOutboundNetworkPolicy(OutboundNetworkPolicy.PublicOnly));

            HttpRequestException error = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync($"http://127.0.0.1:{server.Port}/", Ct));

            OutboundNetworkPolicyException refusal = Assert.IsType<OutboundNetworkPolicyException>(error.InnerException);
            Assert.Equal(OutboundRefusalReason.Address, refusal.Reason);
            Assert.Contains("no address it resolves to is allowed", refusal.Message, StringComparison.Ordinal);
        }
    }
}
