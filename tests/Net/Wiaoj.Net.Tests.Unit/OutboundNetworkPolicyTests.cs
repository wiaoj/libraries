using System.Net;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Blocked networks win, allowed networks are exceptions, scopes decide the rest (#121).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "OutboundNetworkPolicy")]
public sealed class OutboundNetworkPolicyTests {
    private static bool Allowed(OutboundNetworkPolicy policy, string address) => policy.IsAllowed(IPAddress.Parse(address));

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("10.0.0.1", false)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("::ffff:169.254.169.254", false)]
    [InlineData("2002:a9fe:a9fe::", false)]
    [InlineData("fd00::1", false)]
    public void Should_Allow_Only_Public_Addresses_By_Default(string address, bool allowed) {
        Assert.Equal(allowed, Allowed(OutboundNetworkPolicy.PublicOnly, address));
    }

    [Fact]
    public void Should_Allow_An_Internal_Network_Named_As_An_Exception() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] };

        Assert.True(Allowed(policy, "10.20.5.1"));
        Assert.True(Allowed(policy, "::ffff:10.20.5.1"));
        Assert.False(Allowed(policy, "10.21.0.1"));
        Assert.False(Allowed(policy, "169.254.169.254"));
    }

    [Theory]
    // Added for #133: a translated address reaches the IPv4 host it carries, so the exception applies to it.
    [InlineData("64:ff9b::a14:501", true)]
    [InlineData("::ffff:0:a14:501", true)]
    // A tunnel's carried address is an endpoint, not the host reached, so it grants nothing.
    [InlineData("2002:a14:501::1", false)]
    [InlineData("2606:4700::5efe:a14:501", false)]
    [InlineData("2001:0:4136:e378:8000:63bf:f5eb:fafe", false)]
    public void Should_Apply_An_Allowed_Network_Only_To_The_Destination_A_Translated_Address_Carries(string address, bool allowed) {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] };

        Assert.Equal(allowed, Allowed(policy, address));
    }

    [Theory]
    [InlineData("::ffff:0:a9fe:a9fe")]
    [InlineData("2606:4700::5efe:a9fe:a9fe")]
    [InlineData("2002:808:808:1:0:5efe:a9fe:a9fe")]
    public void Should_Refuse_A_Blocked_IPv4_Carried_By_The_Remaining_Forms(string address) {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.Unrestricted with { BlockedNetworks = [IPNetwork.Parse("169.254.169.254/32")] };

        Assert.False(Allowed(policy, address));
    }

    [Fact]
    public void Should_Match_A_Network_Written_As_IPv4_Mapped_IPv6() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("::ffff:10.20.0.0/112")] };

        Assert.True(Allowed(policy, "::ffff:10.20.5.1"));
    }

    [Fact]
    public void Should_Refuse_A_Blocked_Network_Even_When_Its_Scope_Or_An_Exception_Allows_It() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.Unrestricted with {
            AllowedNetworks = [IPNetwork.Parse("169.254.0.0/16")],
            BlockedNetworks = [IPNetwork.Parse("169.254.169.254/32")]
        };

        Assert.False(Allowed(policy, "169.254.169.254"));
        Assert.True(Allowed(policy, "169.254.1.1"));
    }

    [Theory]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("2002:a9fe:a9fe::")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    public void Should_Refuse_A_Blocked_IPv4_Written_Inside_IPv6(string address) {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.Unrestricted with { BlockedNetworks = [IPNetwork.Parse("169.254.169.254/32")] };

        Assert.False(Allowed(policy, address));
    }

    [Fact]
    public void Should_Allow_A_Scope_Added_To_The_Policy() {
        OutboundNetworkPolicy policy = OutboundNetworkPolicy.PublicOnly with {
            AllowedScopes = new HashSet<IPAddressScope> { IPAddressScope.Public, IPAddressScope.Private }
        };

        Assert.True(Allowed(policy, "192.168.1.10"));
        Assert.False(Allowed(policy, "127.0.0.1"));
    }

    [Fact]
    public void Should_Allow_Every_Address_When_Unrestricted() {
        foreach(string address in new[] { "127.0.0.1", "169.254.169.254", "::1", "255.255.255.255", "8.8.8.8" }) {
            Assert.True(Allowed(OutboundNetworkPolicy.Unrestricted, address), address);
        }
    }

    [Fact]
    public void Should_Leave_The_Preset_Unchanged_When_Deriving_A_Policy() {
        HashSet<IPAddressScope> scopes = [IPAddressScope.Public];
        OutboundNetworkPolicy derived = OutboundNetworkPolicy.PublicOnly with { AllowedScopes = scopes };
        scopes.Add(IPAddressScope.Loopback);

        Assert.False(Allowed(OutboundNetworkPolicy.PublicOnly, "127.0.0.1"));
        Assert.False(Allowed(derived, "127.0.0.1"));
    }
}
