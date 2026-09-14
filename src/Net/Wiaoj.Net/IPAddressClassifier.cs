using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Assigns an <see cref="IPAddressScope"/> to an IP address, looking through the ways an IPv4 address can be written
/// inside an IPv6 one.
/// </summary>
/// <remarks>
/// <para>
/// A destination check that only looks at the literal address is bypassed by rewriting it:
/// <c>::ffff:169.254.169.254</c>, <c>2002:a9fe:a9fe::</c> (6to4) and <c>64:ff9b::a9fe:a9fe</c> (NAT64) all reach the
/// cloud metadata endpoint. IPv4-mapped addresses are classified as their IPv4 address; for 6to4, NAT64 and Teredo, a
/// special-purpose embedded IPv4 address decides the scope.
/// </para>
/// <para>
/// A pure function over a fixed table, so it is static and needs no configuration.
/// </para>
/// </remarks>
public static class IPAddressClassifier {
    private static readonly (IPNetwork Network, IPAddressScope Scope)[] IPv4Ranges = [
        (IPNetwork.Parse("0.0.0.0/8"), IPAddressScope.Unspecified),
        (IPNetwork.Parse("10.0.0.0/8"), IPAddressScope.Private),
        (IPNetwork.Parse("100.64.0.0/10"), IPAddressScope.CarrierGradeNat),
        (IPNetwork.Parse("127.0.0.0/8"), IPAddressScope.Loopback),
        (IPNetwork.Parse("169.254.0.0/16"), IPAddressScope.LinkLocal),
        (IPNetwork.Parse("172.16.0.0/12"), IPAddressScope.Private),
        (IPNetwork.Parse("192.0.0.0/24"), IPAddressScope.Reserved),
        (IPNetwork.Parse("192.0.2.0/24"), IPAddressScope.Documentation),
        (IPNetwork.Parse("192.88.99.0/24"), IPAddressScope.Reserved),
        (IPNetwork.Parse("192.168.0.0/16"), IPAddressScope.Private),
        (IPNetwork.Parse("198.18.0.0/15"), IPAddressScope.Benchmarking),
        (IPNetwork.Parse("198.51.100.0/24"), IPAddressScope.Documentation),
        (IPNetwork.Parse("203.0.113.0/24"), IPAddressScope.Documentation),
        (IPNetwork.Parse("224.0.0.0/4"), IPAddressScope.Multicast),
        (IPNetwork.Parse("255.255.255.255/32"), IPAddressScope.Broadcast),
        (IPNetwork.Parse("240.0.0.0/4"), IPAddressScope.Reserved)
    ];

    private static readonly (IPNetwork Network, IPAddressScope Scope)[] IPv6Ranges = [
        (IPNetwork.Parse("::/128"), IPAddressScope.Unspecified),
        (IPNetwork.Parse("::1/128"), IPAddressScope.Loopback),
        (IPNetwork.Parse("::/96"), IPAddressScope.Reserved),
        (IPNetwork.Parse("100::/64"), IPAddressScope.Reserved),
        (IPNetwork.Parse("2001:2::/48"), IPAddressScope.Benchmarking),
        (IPNetwork.Parse("2001:db8::/32"), IPAddressScope.Documentation),
        (IPNetwork.Parse("3fff::/20"), IPAddressScope.Documentation),
        (IPNetwork.Parse("fc00::/7"), IPAddressScope.Private),
        (IPNetwork.Parse("fe80::/10"), IPAddressScope.LinkLocal),
        (IPNetwork.Parse("fec0::/10"), IPAddressScope.Private),
        (IPNetwork.Parse("ff00::/8"), IPAddressScope.Multicast)
    ];

    /// <summary>
    /// Returns the scope of <paramref name="address"/>.
    /// </summary>
    /// <param name="address">An IPv4 or IPv6 address.</param>
    /// <returns>The scope; <see cref="IPAddressScope.Reserved"/> for an address family that is neither.</returns>
    public static IPAddressScope Classify(IPAddress address) {
        Preca.ThrowIfNull(address);

        if(address.IsIPv4MappedToIPv6) {
            return ClassifyIPv4(address.MapToIPv4());
        }

        return address.AddressFamily switch {
            AddressFamily.InterNetwork => ClassifyIPv4(address),
            AddressFamily.InterNetworkV6 => ClassifyIPv6(address),
            _ => IPAddressScope.Reserved
        };
    }

    /// <summary>
    /// Returns the IPv4 address <paramref name="address"/> carries — an IPv4-mapped, 6to4, NAT64 or Teredo address —
    /// or the address itself when it is IPv4.
    /// </summary>
    /// <param name="address">An IP address.</param>
    /// <param name="ipv4">The IPv4 address, when there is one.</param>
    /// <returns><see langword="true"/> when an IPv4 address was found.</returns>
    public static bool TryGetIPv4(IPAddress address, [NotNullWhen(true)] out IPAddress? ipv4) {
        Preca.ThrowIfNull(address);

        if(address.AddressFamily == AddressFamily.InterNetwork) {
            ipv4 = address;
            return true;
        }

        if(address.IsIPv4MappedToIPv6) {
            ipv4 = address.MapToIPv4();
            return true;
        }

        return TryGetTunneledIPv4(address, out ipv4);
    }

    private static IPAddressScope ClassifyIPv4(IPAddress address) {
        foreach((IPNetwork network, IPAddressScope scope) in IPv4Ranges) {
            if(network.Contains(address)) {
                return scope;
            }
        }

        return IPAddressScope.Public;
    }

    private static IPAddressScope ClassifyIPv6(IPAddress address) {
        foreach((IPNetwork network, IPAddressScope scope) in IPv6Ranges) {
            if(network.Contains(address)) {
                return scope;
            }
        }

        // The tunnel prefixes are themselves public; the IPv4 address inside decides.
        return TryGetTunneledIPv4(address, out IPAddress? tunneled)
            ? ClassifyIPv4(tunneled)
            : IPAddressScope.Public;
    }

    private static bool TryGetTunneledIPv4(IPAddress ipv6, [NotNullWhen(true)] out IPAddress? ipv4) {
        Span<byte> bytes = stackalloc byte[16];
        if(ipv6.AddressFamily != AddressFamily.InterNetworkV6 || !ipv6.TryWriteBytes(bytes, out _)) {
            ipv4 = null;
            return false;
        }

        // 6to4, 2002::/16 (RFC 3056): the IPv4 address follows the prefix.
        if(bytes[0] == 0x20 && bytes[1] == 0x02) {
            ipv4 = new IPAddress(bytes.Slice(2, 4));
            return true;
        }

        // NAT64 well-known prefix, 64:ff9b::/96 (RFC 6052): the IPv4 address is the last 32 bits.
        if(bytes[..12].SequenceEqual((ReadOnlySpan<byte>)[0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0])) {
            ipv4 = new IPAddress(bytes.Slice(12, 4));
            return true;
        }

        // Teredo, 2001::/32 (RFC 4380): the client's IPv4 address is the last 32 bits, inverted.
        if(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00) {
            Span<byte> client = [(byte)~bytes[12], (byte)~bytes[13], (byte)~bytes[14], (byte)~bytes[15]];
            ipv4 = new IPAddress(client);
            return true;
        }

        ipv4 = null;
        return false;
    }
}
