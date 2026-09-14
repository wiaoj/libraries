// WebhookIpFilter is obsolete; these tests only pin that it still forwards to Wiaoj.Net until it is removed.
// The SSRF cases themselves live in Wiaoj.Net.Tests.Unit/Ssrf (moved from here).
#pragma warning disable CS0618
using System.Net;
using Wiaoj.Net;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "IpFilter")]
public sealed class WebhookIpFilterForwardingTests {
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("10.0.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("::1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("2002:a9fe:a9fe::")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("255.255.255.255")]
    public void Should_Decide_Exactly_As_The_Policy_It_Forwards_To(string address) {
        IPAddress ip = IPAddress.Parse(address);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip), WebhookIpFilter.IsAllowed(ip));
        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip), WebhookIpFilter.IsAllowed(ip, allowPrivateNetworks: false));
        Assert.Equal(OutboundNetworkPolicy.Unrestricted.IsAllowed(ip), WebhookIpFilter.IsAllowed(ip, allowPrivateNetworks: true));
    }

    [Fact]
    public void Should_Refuse_A_Null_Address() {
        Assert.ThrowsAny<ArgumentNullException>(() => WebhookIpFilter.IsAllowed(null!));
    }
}
