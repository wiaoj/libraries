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
/// <c>::ffff:169.254.169.254</c>, <c>2002:a9fe:a9fe::</c> (6to4), <c>64:ff9b::a9fe:a9fe</c> (NAT64) and
/// <c>::ffff:0:a9fe:a9fe</c> (SIIT) all reach the cloud metadata endpoint. IPv4-mapped addresses are classified as their
/// IPv4 address; for 6to4, NAT64, SIIT, Teredo and ISATAP, a special-purpose embedded IPv4 address decides the scope.
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
        // Local-use IPv4/IPv6 translation (RFC 8215): translates to IPv4 destinations inside the operator's network, with
        // an embedding the RFC leaves undefined, so it cannot be looked through — and IANA lists it as not globally
        // reachable, so refusing it loses no public destination.
        (IPNetwork.Parse("64:ff9b:1::/48"), IPAddressScope.Reserved),
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
    /// Returns the IPv4 address <paramref name="address"/> carries — an IPv4-mapped, 6to4, NAT64, SIIT, Teredo or
    /// ISATAP address — or the address itself when it is IPv4. When a prefix and an ISATAP interface identifier both
    /// carry one, the prefix's is returned.
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

        // The tunnel and translation prefixes are themselves public; the IPv4 addresses inside decide. An address can
        // carry two (a 6to4 prefix with an ISATAP interface identifier), so the first one that is not public wins.
        Span<IPAddress?> carried = [null, null];
        int count = GetCarriedIPv4(address, carried);
        for(int i = 0; i < count; i++) {
            IPAddressScope scope = ClassifyIPv4(carried[i]!);
            if(scope != IPAddressScope.Public) {
                return scope;
            }
        }

        return IPAddressScope.Public;
    }

    /// <summary>
    /// Writes every IPv4 address <paramref name="ipv6"/> carries — at most two — and returns how many there are.
    /// </summary>
    internal static int GetCarriedIPv4(IPAddress ipv6, Span<IPAddress?> carried) {
        Span<byte> bytes = stackalloc byte[16];
        if(ipv6.AddressFamily != AddressFamily.InterNetworkV6 || !ipv6.TryWriteBytes(bytes, out _)) {
            return 0;
        }

        int count = 0;

        if(TryGetPrefixEmbeddedIPv4(bytes, out IPAddress? prefixed)) {
            carried[count++] = prefixed;
        }

        if(TryGetIsatapIPv4(bytes, out IPAddress? isatap)) {
            carried[count++] = isatap;
        }

        return count;
    }

    /// <summary>
    /// Returns the IPv4 destination of a translated address — NAT64 with the well-known prefix (RFC 6052) or
    /// IPv4-translated (SIIT, RFC 2765) — where the carried address is the host actually reached, unlike a tunnel's.
    /// </summary>
    internal static bool TryGetTranslatedIPv4(IPAddress address, [NotNullWhen(true)] out IPAddress? ipv4) {
        Span<byte> bytes = stackalloc byte[16];
        if(address.AddressFamily == AddressFamily.InterNetworkV6 && address.TryWriteBytes(bytes, out _)
           && (IsNat64WellKnown(bytes) || IsIPv4Translated(bytes))) {
            ipv4 = new IPAddress(bytes.Slice(12, 4));
            return true;
        }

        ipv4 = null;
        return false;
    }

    private static bool TryGetTunneledIPv4(IPAddress ipv6, [NotNullWhen(true)] out IPAddress? ipv4) {
        Span<IPAddress?> carried = [null, null];
        int count = GetCarriedIPv4(ipv6, carried);
        ipv4 = count > 0 ? carried[0] : null;
        return ipv4 is not null;
    }

    /// <summary>An IPv4 address carried by the address's prefix: 6to4, NAT64 (well-known prefix), SIIT or Teredo.</summary>
    private static bool TryGetPrefixEmbeddedIPv4(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out IPAddress? ipv4) {
        // 6to4, 2002::/16 (RFC 3056): the IPv4 address follows the prefix.
        if(bytes[0] == 0x20 && bytes[1] == 0x02) {
            ipv4 = new IPAddress(bytes.Slice(2, 4));
            return true;
        }

        // NAT64 well-known prefix 64:ff9b::/96 (RFC 6052), and IPv4-translated ::ffff:0:0:0/96 (SIIT, RFC 2765 §2.1):
        // the IPv4 address is the last 32 bits.
        if(IsNat64WellKnown(bytes) || IsIPv4Translated(bytes)) {
            ipv4 = new IPAddress(bytes.Slice(12, 4));
            return true;
        }

        // Teredo, 2001::/32 (RFC 4380): the client's IPv4 address is the last 32 bits, inverted.
        if(bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00) {
            ipv4 = new IPAddress((ReadOnlySpan<byte>)[(byte)~bytes[12], (byte)~bytes[13], (byte)~bytes[14], (byte)~bytes[15]]);
            return true;
        }

        ipv4 = null;
        return false;
    }

    /// <summary>
    /// ISATAP (RFC 5214 §6.1): an interface identifier of <c>000000ug 00000000 : 5EFE</c> followed by the IPv4 address,
    /// under any prefix — so it can combine with a prefix that carries another IPv4 address.
    /// </summary>
    private static bool TryGetIsatapIPv4(ReadOnlySpan<byte> bytes, [NotNullWhen(true)] out IPAddress? ipv4) {
        // Only the u (0x02) and g (0x01) bits of the first octet may be set.
        if((bytes[8] & 0xFC) == 0 && bytes[9] == 0x00 && bytes[10] == 0x5E && bytes[11] == 0xFE) {
            ipv4 = new IPAddress(bytes.Slice(12, 4));
            return true;
        }

        ipv4 = null;
        return false;
    }

    private static bool IsNat64WellKnown(ReadOnlySpan<byte> bytes) {
        return bytes[..12].SequenceEqual((ReadOnlySpan<byte>)[0x00, 0x64, 0xFF, 0x9B, 0, 0, 0, 0, 0, 0, 0, 0]);
    }

    private static bool IsIPv4Translated(ReadOnlySpan<byte> bytes) {
        return bytes[..12].SequenceEqual((ReadOnlySpan<byte>)[0, 0, 0, 0, 0, 0, 0, 0, 0xFF, 0xFF, 0, 0]);
    }
}
