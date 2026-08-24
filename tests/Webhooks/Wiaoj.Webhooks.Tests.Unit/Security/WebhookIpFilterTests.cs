using System.Net;
using Wiaoj.Webhooks.Security;

namespace Wiaoj.Webhooks.Tests.Unit.Security;

[Trait("Category", "Unit")]
[Trait("Feature", "Security")]
[Trait("Component", "IpFilter")]
public sealed class WebhookIpFilterTests {

    public sealed class PublicValidIpAddresses {
        [Theory]
        [InlineData("8.8.8.8")]              // Google DNS
        [InlineData("1.1.1.1")]              // Cloudflare DNS
        [InlineData("93.184.216.34")]        // example.com
        [InlineData("2606:4700:4700::1111")] // Cloudflare public IPv6
        public void IsAllowed_ReturnsTrue_ForPublicInternetAddresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class PrivateAndLoopbackIPv4Addresses {
        [Theory]
        [InlineData("127.0.0.1")]        // Loopback
        [InlineData("127.255.255.255")]  // Loopback range
        [InlineData("10.0.0.1")]         // RFC 1918 private, Class A
        [InlineData("172.16.0.1")]       // RFC 1918 private, Class B
        [InlineData("172.31.255.255")]   // RFC 1918 private, Class B
        [InlineData("192.168.1.1")]      // RFC 1918 private, Class C
        [InlineData("169.254.169.254")]  // AWS/Azure/GCP cloud metadata endpoint
        [InlineData("169.254.1.1")]      // Link-local
        [InlineData("0.0.0.0")]          // Current network
        [InlineData("255.255.255.255")]  // Broadcast
        public void IsAllowed_ReturnsFalse_ForPrivateAndSpecialIPv4(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class EmbeddedIPv4TunnelingBypasses {
        [Theory]
        [InlineData("::ffff:127.0.0.1")]         // IPv4-mapped IPv6 localhost
        [InlineData("::ffff:169.254.169.254")]   // IPv4-mapped IPv6 AWS metadata
        [InlineData("::ffff:10.0.0.1")]          // IPv4-mapped IPv6 private range
        [InlineData("2002:7f00:0001::")]         // 6to4 tunnel embedding 127.0.0.1
        [InlineData("2002:a9fe:a9fe::")]         // 6to4 tunnel embedding 169.254.169.254
        [InlineData("64:ff9b::127.0.0.1")]       // NAT64 embedding localhost
        [InlineData("64:ff9b::169.254.169.254")] // NAT64 embedding AWS metadata
        [InlineData("2001:0000:4136:e378:8000:63bf:3fff:fdfe")] // Teredo embedding 192.0.2.1 (TEST-NET-1), RFC 4380 bit-complement obfuscated
        public void IsAllowed_ReturnsFalse_ForIPv6EmbeddedIPv4Attacks(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result, $"Attack vector '{ipString}' should have been blocked!");
        }

        [Fact]
        public void IsAllowed_ReturnsTrue_For6to4Tunnel_EmbeddingPublicIp() {
            // Arrange
            // 2002:0808:0808:: -> 8.8.8.8 (public Google DNS)
            IPAddress ip = IPAddress.Parse("2002:0808:0808::");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class PrivateIPv6Addresses {
        [Theory]
        [InlineData("::1")]          // IPv6 loopback
        [InlineData("fe80::1")]      // IPv6 link-local
        [InlineData("fc00::1")]      // IPv6 unique local address (private)
        [InlineData("fd12:3456::1")] // IPv6 unique local address (private)
        [InlineData("fec0::1")]      // Deprecated site-local
        public void IsAllowed_ReturnsFalse_ForPrivateAndLoopbackIPv6(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class DevelopmentBypassFlag {
        [Fact]
        public void IsAllowed_ReturnsTrue_WhenAllowPrivateNetworksIsTrue() {
            // Arrange
            IPAddress localhost = IPAddress.Parse("127.0.0.1");
            IPAddress metadata = IPAddress.Parse("169.254.169.254");

            // Act
            bool localhostResult = WebhookIpFilter.IsAllowed(localhost, allowPrivateNetworks: true);
            bool metadataResult = WebhookIpFilter.IsAllowed(metadata, allowPrivateNetworks: true);

            // Assert
            Assert.True(localhostResult);
            Assert.True(metadataResult);
        }

        [Fact]
        public void IsAllowed_ReturnsTrue_WhenAllowPrivateNetworksIsTrue_ForTunneledAddresses() {
            // Arrange
            // The bypass flag must short-circuit before any embedded-IPv4 unwrapping happens.
            IPAddress sixToFour = IPAddress.Parse("2002:7f00:0001::");

            // Act
            bool result = WebhookIpFilter.IsAllowed(sixToFour, allowPrivateNetworks: true);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsAllowed_ReturnsTrue_WhenAllowPrivateNetworksIsTrue_ForPublicAddress() {
            // Arrange
            // Sanity check: the bypass flag should not somehow break normal public addresses either.
            IPAddress publicIp = IPAddress.Parse("8.8.8.8");

            // Act
            bool result = WebhookIpFilter.IsAllowed(publicIp, allowPrivateNetworks: true);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class TestNetDocumentationRanges {
        [Theory]
        [InlineData("192.0.2.1")]    // TEST-NET-1
        [InlineData("192.0.2.255")]  // TEST-NET-1, last address
        [InlineData("198.51.100.1")] // TEST-NET-2
        [InlineData("203.0.113.1")]  // TEST-NET-3
        public void IsAllowed_ReturnsFalse_ForRfc5737TestNetAddresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class CarrierGradeNatRange {
        [Theory]
        [InlineData("100.64.0.1")]
        [InlineData("100.100.100.100")]
        [InlineData("100.127.255.255")]
        public void IsAllowed_ReturnsFalse_ForCgnatAddresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }

        [Theory]
        [InlineData("100.63.255.255")] // just below the CGNAT range -> should be public
        [InlineData("100.128.0.0")]    // just above the CGNAT range -> should be public
        public void IsAllowed_ReturnsTrue_JustOutsideCgnatBoundaries(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class Rfc1918BoundaryChecks {
        [Theory]
        [InlineData("172.15.255.255")]  // just below 172.16.0.0/12 -> public
        [InlineData("172.32.0.0")]      // just above 172.16.0.0/12 -> public
        [InlineData("9.255.255.255")]   // just below 10.0.0.0/8 -> public
        [InlineData("11.0.0.0")]        // just above 10.0.0.0/8 -> public
        [InlineData("192.167.255.255")] // just below 192.168.0.0/16 -> public
        [InlineData("192.169.0.0")]     // just above 192.168.0.0/16 -> public
        public void IsAllowed_ReturnsTrue_JustOutsidePrivateBoundaries(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData("172.16.0.0")]     // first address of the range
        [InlineData("10.255.255.255")] // last address of the range
        [InlineData("192.168.0.0")]    // first address of the range
        public void IsAllowed_ReturnsFalse_AtExactPrivateBoundaries(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class MulticastReservedAndBenchmarking {
        [Theory]
        [InlineData("224.0.0.1")]       // multicast
        [InlineData("239.255.255.255")] // multicast, upper bound
        [InlineData("240.0.0.1")]       // reserved
        [InlineData("198.18.0.1")]      // RFC 2544 benchmarking
        [InlineData("198.19.255.255")]  // RFC 2544 benchmarking, upper bound
        [InlineData("192.88.99.1")]     // RFC 3068 6to4 relay anycast
        public void IsAllowed_ReturnsFalse_ForMulticastReservedAndBenchmarkAddresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class SpecialIPv6Ranges {
        [Theory]
        [InlineData("::")]          // unspecified address
        [InlineData("ff02::1")]     // multicast, all-nodes
        [InlineData("ff05::1:3")]   // multicast, site-local scope
        [InlineData("100::1")]      // discard-only (RFC 6666)
        [InlineData("2001:db8::1")] // documentation prefix
        public void IsAllowed_ReturnsFalse_ForSpecialIPv6Addresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class Nat64PublicAddressPassthrough {
        [Fact]
        public void IsAllowed_ReturnsTrue_ForNat64Tunnel_EmbeddingPublicIp() {
            // Arrange
            // 64:ff9b::8.8.8.8 -> embedded address is public Google DNS
            IPAddress ip = IPAddress.Parse("64:ff9b::8.8.8.8");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class IPv4MappedPublicAddresses {
        [Theory]
        [InlineData("::ffff:8.8.8.8")]
        [InlineData("::ffff:1.1.1.1")]
        public void IsAllowed_ReturnsTrue_ForIPv4MappedPublicAddresses(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class TeredoEmbeddedIPv4Resolution {
        [Theory]
        [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")] // canonical RFC 4380 example, embeds 192.0.2.45 (TEST-NET-1)
        [InlineData("2001::1")]                               // minimal Teredo address, embeds 255.255.255.254 (RFC 1112 reserved)
        public void IsAllowed_ReturnsFalse_ForTeredoTunnel_EmbeddingReservedAddress(string ipString) {
            // Arrange
            IPAddress ip = IPAddress.Parse(ipString);

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAllowed_ReturnsTrue_ForTeredoTunnel_EmbeddingPublicAddress() {
            // Arrange
            // Same server/flags/port groups as the RFC 4380 example above, but the last two groups
            // (f7f7:f7f7) are the bit-complement obfuscation of 8.8.8.8 -> ~8=0xF7 for every octet.
            IPAddress ip = IPAddress.Parse("2001:0:4136:e378:8000:63bf:f7f7:f7f7");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }
    }

    public sealed class SixToFourBoundary {
        [Fact]
        public void IsAllowed_DoesNotMisidentify_NonSixToFourPrefixAsTunnel() {
            // Arrange
            // 2003:: is not the 6to4 prefix (2002::) and should be treated as ordinary global unicast.
            IPAddress ip = IPAddress.Parse("2003::1");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void IsAllowed_ReturnsFalse_ForSixToFourTunnel_AtExactPrefixStart() {
            // Arrange
            // 2002:: itself carries an all-zero embedded IPv4 (0.0.0.0), which is prohibited.
            IPAddress ip = IPAddress.Parse("2002::");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }
    }

    public sealed class InputValidationAndEdgeCases {
        [Fact]
        public void IsAllowed_ReturnsFalse_ForNat64Tunnel_EmbeddingCarrierGradeNatAddress() {
            // Arrange
            // 64:ff9b::100.64.0.1 -> embedded address falls inside the RFC 6598 CGNAT range.
            IPAddress ip = IPAddress.Parse("64:ff9b::100.64.0.1");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAllowed_ReturnsFalse_For6to4Tunnel_EmbeddingTestNetAddress() {
            // Arrange
            // 2002:c000:0201:: -> embedded 192.0.2.1 (TEST-NET-1)
            IPAddress ip = IPAddress.Parse("2002:c000:0201::");

            // Act
            bool result = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsAllowed_IsConsistent_AcrossRepeatedCallsForSameAddress() {
            // Arrange
            IPAddress ip = IPAddress.Parse("169.254.169.254");

            // Act
            bool firstCall = WebhookIpFilter.IsAllowed(ip);
            bool secondCall = WebhookIpFilter.IsAllowed(ip);

            // Assert
            Assert.False(firstCall);
            Assert.Equal(firstCall, secondCall);
        }

        [Fact]
        public void IsAllowed_RespectsAllowPrivateNetworksFlag_ForSameAddress() {
            // Arrange
            // Only the "flag = true" branch adds new coverage here; the "flag = false" case
            // for 10.0.0.1 is already exercised by PrivateAndLoopbackIPv4Addresses, so we don't
            // repeat it — this test isolates what the flag itself changes for one fixed address.
            IPAddress ip = IPAddress.Parse("10.0.0.1");

            // Act
            bool blockedByDefault = WebhookIpFilter.IsAllowed(ip, allowPrivateNetworks: false);
            bool allowedWithBypass = WebhookIpFilter.IsAllowed(ip, allowPrivateNetworks: true);

            // Assert
            Assert.False(blockedByDefault);
            Assert.True(allowedWithBypass);
        }
    }
}