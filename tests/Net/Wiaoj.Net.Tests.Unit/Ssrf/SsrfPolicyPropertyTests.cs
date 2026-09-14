// Moved from Wiaoj.Webhooks.Tests.Unit (WebhookIpFilter*, RealWorldSsrfPayloadsTests) when the filter moved to Wiaoj.Net.
// Every case is unchanged; only the subject changed: WebhookIpFilter.IsAllowed(ip) -> OutboundNetworkPolicy.PublicOnly.IsAllowed(ip),
// IsAllowed(ip, allowPrivateNetworks: true) -> OutboundNetworkPolicy.Unrestricted.IsAllowed(ip).
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using System.Net;

namespace Wiaoj.Net.Tests.Unit.Ssrf;

/// <summary>
/// Property-based tests for <see cref="OutboundNetworkPolicy"/> (formerly WebhookIpFilter), using FsCheck instead of hand-picked
/// example values. These complement — they do NOT replace — the example-based tests in
/// <c>SsrfPolicyTests</c>.
///
/// Two testing strategies are used here, each with a different (and limited) guarantee:
///
/// 1. Metamorphic / round-trip properties: an IPv4 address is independently re-encoded into a
///    tunneling format (6to4, NAT64, Teredo, IPv4-mapped IPv6) using logic written directly from
///    the RFCs — not copied from IPAddressClassifier's own extraction code — and we assert that
///    encoding-then-filtering agrees with filtering the original address. This catches
///    implementation bugs (off-by-one byte slicing, wrong bit position, wrong prefix check).
///
/// 2. An independently written reference oracle (raw uint32 arithmetic instead of IPNetwork/CIDR
///    objects) that answers "is this IPv4 reserved?" and is compared against IsAllowed.
///
/// IMPORTANT CAVEAT: both the oracle and the embedding helpers below were written by the same
/// author as the production code, based on the same understanding of the relevant RFCs. If that
/// understanding is wrong (e.g. a misremembered CIDR boundary, a misread RFC), the mistake can be
/// reproduced in both places and these tests will pass without catching it. Property tests reduce
/// implementation-bug risk; they do not substitute for independently-sourced test vectors, a
/// second reviewer, or actually running the suite.
/// </summary>
[Trait("Category", "Property")]
[Trait("Feature", "Security")]
[Trait("Component", "IpFilter")]
public sealed class SsrfPolicyPropertyTests {

    // ─────────────────────────────────────────────────────────────────────────
    // Independent reference oracle (raw arithmetic, not IPNetwork/CIDR objects)
    // ─────────────────────────────────────────────────────────────────────────

    private static bool IsKnownReservedIPv4(byte b1, byte b2, byte b3, byte b4) {
        uint ip = ((uint)b1 << 24) | ((uint)b2 << 16) | ((uint)b3 << 8) | b4;

        static bool InRange(uint value, uint start, uint end) => value >= start && value <= end;

        return
            InRange(ip, 0x00000000, 0x00FFFFFF) || // 0.0.0.0/8
            InRange(ip, 0x0A000000, 0x0AFFFFFF) || // 10.0.0.0/8
            InRange(ip, 0x64400000, 0x647FFFFF) || // 100.64.0.0/10
            InRange(ip, 0x7F000000, 0x7FFFFFFF) || // 127.0.0.0/8
            InRange(ip, 0xA9FE0000, 0xA9FEFFFF) || // 169.254.0.0/16
            InRange(ip, 0xAC100000, 0xAC1FFFFF) || // 172.16.0.0/12
            InRange(ip, 0xC0000000, 0xC00000FF) || // 192.0.0.0/24
            InRange(ip, 0xC0000200, 0xC00002FF) || // 192.0.2.0/24
            InRange(ip, 0xC0586300, 0xC05863FF) || // 192.88.99.0/24
            InRange(ip, 0xC0A80000, 0xC0A8FFFF) || // 192.168.0.0/16
            InRange(ip, 0xC6120000, 0xC613FFFF) || // 198.18.0.0/15
            InRange(ip, 0xC6336400, 0xC63364FF) || // 198.51.100.0/24
            InRange(ip, 0xCB007100, 0xCB0071FF) || // 203.0.113.0/24
            InRange(ip, 0xE0000000, 0xEFFFFFFF) || // 224.0.0.0/4
            InRange(ip, 0xF0000000, 0xFFFFFFFF);   // 240.0.0.0/4 (includes 255.255.255.255)
    }

    [Property(MaxTest = 1000)]
    public Property IsAllowed_AgreesWithIndependentReservedRangeOracle_ForAnyIPv4(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ip = new([b1, b2, b3, b4]);
        bool expected = !IsKnownReservedIPv4(b1, b2, b3, b4);
        bool actual = OutboundNetworkPolicy.PublicOnly.IsAllowed(ip);

        return (actual == expected)
            .Label($"{ip} -> IsAllowed returned {actual}, oracle expected {expected}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Range invariants: every address inside a known-private CIDR must be blocked,
    // sampled across the full range rather than a couple of hand-picked corners.
    // ─────────────────────────────────────────────────────────────────────────

    [Property]
    public void IsAllowed_ReturnsFalse_ForAnyAddress_In_10_0_0_0_8(byte b2, byte b3, byte b4) {
        IPAddress ip = new([10, b2, b3, b4]);
        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip));
    }

    [Property]
    public void IsAllowed_ReturnsFalse_ForAnyAddress_In_127_0_0_0_8(byte b2, byte b3, byte b4) {
        IPAddress ip = new([127, b2, b3, b4]);
        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip));
    }

    [Property]
    public void IsAllowed_ReturnsFalse_ForAnyAddress_In_169_254_0_0_16(byte b3, byte b4) {
        IPAddress ip = new([169, 254, b3, b4]);
        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip));
    }

    [Property]
    public void IsAllowed_ReturnsFalse_ForAnyAddress_In_192_168_0_0_16(byte b3, byte b4) {
        IPAddress ip = new([192, 168, b3, b4]);
        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip));
    }

    [Property]
    public void IsAllowed_ReturnsFalse_ForAnyAddress_In_172_16_0_0_12(byte secondOctetSeed, byte b3, byte b4) {
        // 172.16.0.0/12 covers second-octet values 16-31 (top 4 bits fixed to 0001).
        byte b2 = (byte)(16 + (secondOctetSeed % 16));
        IPAddress ip = new([172, b2, b3, b4]);
        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(ip));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Metamorphic properties: re-encoding an IPv4 address into a tunneling format
    // must never change the verdict. Embedding helpers are written from the RFCs
    // directly, independently of IPAddressClassifier's own extraction code.
    // ─────────────────────────────────────────────────────────────────────────

    [Property]
    public void IsAllowed_IsInvariant_UnderIPv4MappedIPv6Encoding(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress mapped = ipv4.MapToIPv6();

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(mapped));
    }

    [Property]
    public void IsAllowed_IsInvariant_UnderSixToFourTunneling(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress tunneled = Embed6to4(b1, b2, b3, b4);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(tunneled));
    }

    [Property]
    public void IsAllowed_IsInvariant_UnderNat64Tunneling(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress tunneled = EmbedNat64(b1, b2, b3, b4);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(tunneled));
    }

    [Property]
    public void IsAllowed_IsInvariant_UnderTeredoTunneling(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress tunneled = EmbedTeredo(b1, b2, b3, b4);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(tunneled));
    }

    // Added for #133: the IPv4-in-IPv6 forms the original suite did not cover.

    [Property]
    public void IsAllowed_IsInvariant_UnderIPv4TranslatedSiitEncoding(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress translated = EmbedSiit(b1, b2, b3, b4);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(translated));
    }

    [Property]
    public void IsAllowed_IsInvariant_UnderIsatapInterfaceIdentifier_UnderAPublicPrefix(byte b1, byte b2, byte b3, byte b4, bool universal) {
        IPAddress ipv4 = new([b1, b2, b3, b4]);
        IPAddress isatap = EmbedIsatap(PublicIsatapPrefix, universal, b1, b2, b3, b4);

        Assert.Equal(OutboundNetworkPolicy.PublicOnly.IsAllowed(ipv4), OutboundNetworkPolicy.PublicOnly.IsAllowed(isatap));
    }

    [Property]
    public void IsAllowed_RefusesAnIsatapIdentifierCarryingARefusedIPv4_EvenUnderA6to4PrefixCarryingAPublicOne(byte b3, byte b4) {
        // Two carried addresses: 6to4's public 8.8.8.8 must not hide ISATAP's 169.254.x.y.
        byte[] prefix = [0x20, 0x02, 8, 8, 8, 8, 0x00, 0x01];
        IPAddress address = EmbedIsatap(prefix, universal: false, 169, 254, b3, b4);

        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(address));
    }

    [Property]
    public void IsAllowed_RefusesEveryAddressInTheLocalUseTranslationPrefix(ulong low, ushort subnet) {
        // RFC 8215: 64:ff9b:1::/48 translates to operator-internal IPv4 with an undefined embedding — never public.
        byte[] bytes = new byte[16];
        bytes[0] = 0x00; bytes[1] = 0x64; bytes[2] = 0xFF; bytes[3] = 0x9B; bytes[4] = 0x00; bytes[5] = 0x01;
        bytes[6] = (byte)(subnet >> 8); bytes[7] = (byte)subnet;
        for(int i = 0; i < 8; i++) {
            bytes[8 + i] = (byte)(low >> (56 - 8 * i));
        }

        Assert.False(OutboundNetworkPolicy.PublicOnly.IsAllowed(new IPAddress(bytes)));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // General invariants
    // ─────────────────────────────────────────────────────────────────────────

    [Property]
    public void IsAllowed_AlwaysReturnsTrue_WhenBypassIsEnabled_ForAnyIPv4(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ip = new([b1, b2, b3, b4]);
        Assert.True(OutboundNetworkPolicy.Unrestricted.IsAllowed(ip));
    }

    [Property]
    public void IsAllowed_IsDeterministic_ForAnyIPv4(byte b1, byte b2, byte b3, byte b4) {
        IPAddress ip = new([b1, b2, b3, b4]);
        bool first = OutboundNetworkPolicy.PublicOnly.IsAllowed(ip);
        bool second = OutboundNetworkPolicy.PublicOnly.IsAllowed(ip);
        Assert.Equal(first, second);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tunnel embedding helpers — written from RFC text (4380, 6052, "6to4"/RFC 3056),
    // deliberately not copied from IPAddressClassifier.TryGetTunneledIPv4.
    // ─────────────────────────────────────────────────────────────────────────

    private static IPAddress Embed6to4(byte b1, byte b2, byte b3, byte b4) {
        byte[] bytes = new byte[16];
        bytes[0] = 0x20;
        bytes[1] = 0x02;
        bytes[2] = b1;
        bytes[3] = b2;
        bytes[4] = b3;
        bytes[5] = b4;
        return new IPAddress(bytes);
    }

    private static IPAddress EmbedNat64(byte b1, byte b2, byte b3, byte b4) {
        byte[] bytes = new byte[16];
        bytes[0] = 0x00;
        bytes[1] = 0x64;
        bytes[2] = 0xFF;
        bytes[3] = 0x9B;
        // bytes[4..12) must stay zero — required by the NAT64 "well-known prefix" (RFC 6052 §2.1)
        bytes[12] = b1;
        bytes[13] = b2;
        bytes[14] = b3;
        bytes[15] = b4;
        return new IPAddress(bytes);
    }

    private static IPAddress EmbedTeredo(byte b1, byte b2, byte b3, byte b4) {
        byte[] bytes = new byte[16];
        bytes[0] = 0x20;
        bytes[1] = 0x01;
        bytes[2] = 0x00;
        bytes[3] = 0x00;
        // bytes[4..12) hold the Teredo server IPv4 + flags + obfuscated port — irrelevant to
        // IPAddressClassifier's client-address extraction, left at zero.
        bytes[12] = (byte)~b1;
        bytes[13] = (byte)~b2;
        bytes[14] = (byte)~b3;
        bytes[15] = (byte)~b4;
        return new IPAddress(bytes);
    }

    /// <summary>A documentation-free, globally routable prefix (Cloudflare's 2606:4700::/32) for ISATAP identifiers.</summary>
    private static readonly byte[] PublicIsatapPrefix = [0x26, 0x06, 0x47, 0x00, 0x00, 0x00, 0x00, 0x01];

    private static IPAddress EmbedSiit(byte b1, byte b2, byte b3, byte b4) {
        // RFC 2765 §2.1: "An address of the form 0::ffff:0:a.b.c.d".
        byte[] bytes = new byte[16];
        bytes[8] = 0xFF;
        bytes[9] = 0xFF;
        bytes[12] = b1;
        bytes[13] = b2;
        bytes[14] = b3;
        bytes[15] = b4;
        return new IPAddress(bytes);
    }

    private static IPAddress EmbedIsatap(byte[] prefix, bool universal, byte b1, byte b2, byte b3, byte b4) {
        // RFC 5214 §6.1: interface identifier 000000ug 00000000 01011110 11111110 followed by the IPv4 address.
        byte[] bytes = new byte[16];
        Array.Copy(prefix, bytes, 8);
        bytes[8] = universal ? (byte)0x02 : (byte)0x00;
        bytes[9] = 0x00;
        bytes[10] = 0x5E;
        bytes[11] = 0xFE;
        bytes[12] = b1;
        bytes[13] = b2;
        bytes[14] = b3;
        bytes[15] = b4;
        return new IPAddress(bytes);
    }
}