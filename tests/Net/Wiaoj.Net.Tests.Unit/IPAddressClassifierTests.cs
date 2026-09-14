using System.Net;

namespace Wiaoj.Net.Tests.Unit;

/// <summary>Every special-purpose range, including IPv4 written inside IPv6 (#121).</summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Net")]
[Trait("Component", "IPAddressClassifier")]
public sealed class IPAddressClassifierTests {
    [Theory]
    [InlineData("8.8.8.8", IPAddressScope.Public)]
    [InlineData("1.1.1.1", IPAddressScope.Public)]
    [InlineData("0.0.0.0", IPAddressScope.Unspecified)]
    [InlineData("0.255.255.255", IPAddressScope.Unspecified)]
    [InlineData("10.0.0.1", IPAddressScope.Private)]
    [InlineData("10.255.255.255", IPAddressScope.Private)]
    [InlineData("11.0.0.1", IPAddressScope.Public)]
    [InlineData("100.64.0.1", IPAddressScope.CarrierGradeNat)]
    [InlineData("100.127.255.255", IPAddressScope.CarrierGradeNat)]
    [InlineData("100.128.0.1", IPAddressScope.Public)]
    [InlineData("127.0.0.1", IPAddressScope.Loopback)]
    [InlineData("127.255.255.254", IPAddressScope.Loopback)]
    [InlineData("169.254.169.254", IPAddressScope.LinkLocal)]
    [InlineData("172.16.0.1", IPAddressScope.Private)]
    [InlineData("172.31.255.255", IPAddressScope.Private)]
    [InlineData("172.32.0.1", IPAddressScope.Public)]
    [InlineData("192.0.0.1", IPAddressScope.Reserved)]
    [InlineData("192.0.2.1", IPAddressScope.Documentation)]
    [InlineData("192.88.99.1", IPAddressScope.Reserved)]
    [InlineData("192.168.1.1", IPAddressScope.Private)]
    [InlineData("198.18.0.1", IPAddressScope.Benchmarking)]
    [InlineData("198.19.255.255", IPAddressScope.Benchmarking)]
    [InlineData("198.51.100.1", IPAddressScope.Documentation)]
    [InlineData("203.0.113.1", IPAddressScope.Documentation)]
    [InlineData("224.0.0.1", IPAddressScope.Multicast)]
    [InlineData("239.255.255.255", IPAddressScope.Multicast)]
    [InlineData("240.0.0.1", IPAddressScope.Reserved)]
    [InlineData("255.255.255.255", IPAddressScope.Broadcast)]
    public void Should_Classify_IPv4(string address, IPAddressScope scope) {
        Assert.Equal(scope, IPAddressClassifier.Classify(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("2606:4700:4700::1111", IPAddressScope.Public)]
    [InlineData("::", IPAddressScope.Unspecified)]
    [InlineData("::1", IPAddressScope.Loopback)]
    [InlineData("::0.0.0.2", IPAddressScope.Reserved)]
    [InlineData("100::1", IPAddressScope.Reserved)]
    [InlineData("2001:2::1", IPAddressScope.Benchmarking)]
    [InlineData("2001:db8::1", IPAddressScope.Documentation)]
    [InlineData("3fff::1", IPAddressScope.Documentation)]
    [InlineData("fc00::1", IPAddressScope.Private)]
    [InlineData("fd12:3456::1", IPAddressScope.Private)]
    [InlineData("fe80::1", IPAddressScope.LinkLocal)]
    [InlineData("fec0::1", IPAddressScope.Private)]
    [InlineData("ff02::1", IPAddressScope.Multicast)]
    public void Should_Classify_IPv6(string address, IPAddressScope scope) {
        Assert.Equal(scope, IPAddressClassifier.Classify(IPAddress.Parse(address)));
    }

    [Theory]
    // IPv4-mapped
    [InlineData("::ffff:169.254.169.254", IPAddressScope.LinkLocal)]
    [InlineData("::ffff:127.0.0.1", IPAddressScope.Loopback)]
    [InlineData("::ffff:8.8.8.8", IPAddressScope.Public)]
    // 6to4 of 169.254.169.254 and of 10.0.0.1
    [InlineData("2002:a9fe:a9fe::", IPAddressScope.LinkLocal)]
    [InlineData("2002:0a00:0001::1", IPAddressScope.Private)]
    [InlineData("2002:0808:0808::1", IPAddressScope.Public)]
    // NAT64 well-known prefix
    [InlineData("64:ff9b::a9fe:a9fe", IPAddressScope.LinkLocal)]
    [InlineData("64:ff9b::7f00:1", IPAddressScope.Loopback)]
    [InlineData("64:ff9b::808:808", IPAddressScope.Public)]
    // Teredo: the last 32 bits are the client address, inverted — ~127.0.0.1 = 80ff:fffe
    [InlineData("2001:0:4136:e378:8000:63bf:80ff:fffe", IPAddressScope.Loopback)]
    public void Should_Classify_IPv4_Carried_Inside_IPv6_As_That_IPv4(string address, IPAddressScope scope) {
        Assert.Equal(scope, IPAddressClassifier.Classify(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("10.1.2.3", "10.1.2.3")]
    [InlineData("::ffff:10.1.2.3", "10.1.2.3")]
    [InlineData("2002:0a01:0203::", "10.1.2.3")]
    [InlineData("64:ff9b::a01:203", "10.1.2.3")]
    public void Should_Extract_The_Carried_IPv4(string address, string ipv4) {
        Assert.True(IPAddressClassifier.TryGetIPv4(IPAddress.Parse(address), out IPAddress? carried));
        Assert.Equal(IPAddress.Parse(ipv4), carried);
    }

    [Fact]
    public void Should_Find_No_IPv4_In_A_Native_IPv6_Address() {
        Assert.False(IPAddressClassifier.TryGetIPv4(IPAddress.Parse("2606:4700:4700::1111"), out _));
    }
}
