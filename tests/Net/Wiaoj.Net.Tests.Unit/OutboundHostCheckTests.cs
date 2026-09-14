using System.Net;
using System.Net.Sockets;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Checking a host before connecting returns a result, never throws for a refused or unknown host (#122).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "OutboundHostCheck")]
public sealed class OutboundHostCheckTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("8.8.8.8", OutboundHostStatus.Allowed)]
    [InlineData("127.0.0.1", OutboundHostStatus.Refused)]
    [InlineData("[::1]", OutboundHostStatus.Refused)]
    [InlineData("169.254.169.254", OutboundHostStatus.Refused)]
    [InlineData("localhost", OutboundHostStatus.Refused)]
    public async Task Should_Decide_Literals_And_Names(string host, OutboundHostStatus status) {
        OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(host, Ct);

        Assert.Equal(status, check.Status);
        Assert.Equal(status == OutboundHostStatus.Allowed, check.IsAllowed);
        Assert.Equal(host, check.Host);
    }

    [Theory]
    [InlineData("http://[::1]:8080/hook", OutboundHostStatus.Refused)]
    [InlineData("https://8.8.8.8/hook", OutboundHostStatus.Allowed)]
    public async Task Should_Check_The_Host_Of_A_Url(string url, OutboundHostStatus status) {
        Assert.Equal(status, (await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(new Uri(url), Ct)).Status);
    }

    [Fact]
    public async Task Should_Return_An_Unresolvable_Host_With_The_Resolver_Error() {
        OutboundHostCheck check = await OutboundNetworkPolicy.PublicOnly.CheckHostAsync("nothing.nonexistent.invalid", Ct);

        Assert.Equal(OutboundHostStatus.Unresolvable, check.Status);
        Assert.IsType<SocketException>(check.ResolutionError);
    }

    [Fact]
    public async Task Should_Allow_A_Host_In_An_Allowed_Network() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("127.0.0.0/8")] };

        Assert.True((await policy.CheckHostAsync("127.0.0.1", Ct)).IsAllowed);
    }

    [Fact]
    public async Task Should_Refuse_A_Relative_Url() {
        await Assert.ThrowsAnyAsync<ArgumentException>(async () => await OutboundNetworkPolicy.PublicOnly.CheckHostAsync(new Uri("/hook", UriKind.Relative), Ct));
    }
}
