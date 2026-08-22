using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Enterprise-grade, high-performance IP validator that defends against SSRF (Server-Side Request Forgery) attacks.
/// Evaluates RFC private ranges, loopback, cloud metadata, and decodes embedded IPv4 tunneling protocols (6to4, NAT64, Teredo).
/// </summary>
public static class WebhookIpFilter {

    private static readonly IPNetwork[] ProhibitedIPv4Networks = [
        IPNetwork.Parse("0.0.0.0/8"),          // "This" network (RFC 1122)
        IPNetwork.Parse("10.0.0.0/8"),         // Private network (RFC 1918)
        IPNetwork.Parse("100.64.0.0/10"),      // Carrier-Grade NAT (RFC 6598)
        IPNetwork.Parse("127.0.0.0/8"),        // Loopback / localhost (RFC 1122)
        IPNetwork.Parse("169.254.0.0/16"),     // Link-local & Cloud Metadata 169.254.169.254 (RFC 3927)
        IPNetwork.Parse("172.16.0.0/12"),      // Private network (RFC 1918)
        IPNetwork.Parse("192.0.0.0/24"),       // IETF Protocol Assignments (RFC 6890)
        IPNetwork.Parse("192.0.2.0/24"),       // TEST-NET-1 (RFC 5737)
        IPNetwork.Parse("192.88.99.0/24"),     // 6to4 Relay Anycast (RFC 7526)
        IPNetwork.Parse("192.168.0.0/16"),     // Private network (RFC 1918)
        IPNetwork.Parse("198.18.0.0/15"),      // Benchmark testing (RFC 2544)
        IPNetwork.Parse("198.51.100.0/24"),    // TEST-NET-2 (RFC 5737)
        IPNetwork.Parse("203.0.113.0/24"),     // TEST-NET-3 (RFC 5737)
        IPNetwork.Parse("224.0.0.0/4"),        // Multicast (RFC 5771)
        IPNetwork.Parse("240.0.0.0/4"),        // Reserved for future use (RFC 1112)
        IPNetwork.Parse("255.255.255.255/32")  // Limited Broadcast (RFC 919)
    ];

    private static readonly IPNetwork[] ProhibitedIPv6Networks = [
        IPNetwork.Parse("::/128"),             // Unspecified (RFC 4291)
        IPNetwork.Parse("::1/128"),            // Loopback (RFC 4291)
        IPNetwork.Parse("100::/64"),           // Discard-Only (RFC 6666)
        IPNetwork.Parse("2001:db8::/32"),      // Documentation (RFC 3849)
        IPNetwork.Parse("fc00::/7"),           // Unique Local Address - Private (RFC 4193)
        IPNetwork.Parse("fe80::/10"),          // Link-Local Unicast (RFC 4291)
        IPNetwork.Parse("fec0::/10"),          // Deprecated Site-Local (RFC 3879)
        IPNetwork.Parse("ff00::/8")            // Multicast (RFC 4291)
    ];

    /// <summary>
    /// Evaluates whether an outbound IP address is safe for webhook transmission.
    /// </summary>
    /// <param name="ipAddress">The IP address to evaluate.</param>
    /// <param name="allowPrivateNetworks">When <see langword="true"/>, disables private network filtering (development mode only).</param>
    /// <returns><see langword="true"/> if the IP address is allowed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(IPAddress ipAddress, bool allowPrivateNetworks = false) {
        if(allowPrivateNetworks) {
            return true;
        }

        // 1. IPv4-Mapped IPv6 validation (e.g. ::ffff:127.0.0.1)
        if(ipAddress.IsIPv4MappedToIPv6) {
            return IsAllowedIPv4(ipAddress.MapToIPv4());
        }

        // 2. IPv4 Verification
        if(ipAddress.AddressFamily == AddressFamily.InterNetwork) {
            return IsAllowedIPv4(ipAddress);
        }

        // 3. IPv6 Verification
        if(ipAddress.AddressFamily == AddressFamily.InterNetworkV6) {
            // A. Check for embedded IPv4 tunnels inside IPv6 (6to4, NAT64, Teredo)
            if(TryExtractEmbeddedIPv4(ipAddress, out IPAddress? embeddedIpv4)) {
                if(!IsAllowedIPv4(embeddedIpv4)) {
                    return false; // The encapsulated IPv4 address points to a private/prohibited network
                }
            }

            // B. Standard IPv6 prohibited network checks
            if(IPAddress.IsLoopback(ipAddress) || ipAddress.IsIPv6LinkLocal || ipAddress.IsIPv6SiteLocal) {
                return false;
            }

            for(int i = 0; i < ProhibitedIPv6Networks.Length; i++) {
                if(ProhibitedIPv6Networks[i].Contains(ipAddress)) {
                    return false;
                }
            }

            return true;
        }

        // Unknown or unsupported address families are rejected by default
        return false;
    }

    private static bool IsAllowedIPv4(IPAddress ipAddress) {
        if(IPAddress.IsLoopback(ipAddress)) {
            return false;
        }

        for(int i = 0; i < ProhibitedIPv4Networks.Length; i++) {
            if(ProhibitedIPv4Networks[i].Contains(ipAddress)) {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Decodes encapsulated IPv4 addresses from 6to4, NAT64, and Teredo IPv6 tunneling protocols.
    /// Uses zero-allocation stackalloc memory.
    /// </summary>
    private static bool TryExtractEmbeddedIPv4(IPAddress ipv6, [NotNullWhen(true)] out IPAddress? embeddedIpv4) {
        Span<byte> bytes = stackalloc byte[16];
        if(!ipv6.TryWriteBytes(bytes, out _)) {
            embeddedIpv4 = null;
            return false;
        }

        // ── 6to4 (2002::/16) -> bytes[2..6] holds IPv4 ──
        if(bytes[0] == 0x20 && bytes[1] == 0x02) {
            embeddedIpv4 = new IPAddress(bytes.Slice(2, 4));
            return true;
        }

        // ── NAT64 Well-Known Prefix (64:ff9b::/96) -> bytes[12..16] holds IPv4 ──
        if(bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xFF && bytes[3] == 0x9B &&
            bytes[4] == 0 && bytes[5] == 0 && bytes[6] == 0 && bytes[7] == 0 &&
            bytes[8] == 0 && bytes[9] == 0 && bytes[10] == 0 && bytes[11] == 0) {
            embeddedIpv4 = new IPAddress(bytes.Slice(12, 4));
            return true;
        }

        // ── Teredo (2001:0000::/32) -> bytes[12..16] holds XORed IPv4 ──
        if(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00) {
            Span<byte> teredoIpv4 = [
                (byte)~bytes[12],
                (byte)~bytes[13],
                (byte)~bytes[14],
                (byte)~bytes[15],
            ];
            embeddedIpv4 = new IPAddress(teredoIpv4);
            return true;
        }

        embeddedIpv4 = null;
        return false;
    }
}