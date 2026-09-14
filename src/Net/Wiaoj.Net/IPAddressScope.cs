namespace Wiaoj.Net;

/// <summary>
/// What kind of network an IP address belongs to — the question an outbound connection policy asks before connecting.
/// </summary>
/// <remarks>
/// Scopes are coarse on purpose: a policy allows or refuses whole kinds of destination, and names specific networks as
/// exceptions. <see cref="IPAddressClassifier"/> assigns them.
/// </remarks>
public enum IPAddressScope {
    /// <summary>Globally routable: none of the special-purpose ranges below.</summary>
    Public,

    /// <summary>The unspecified address or "this network": <c>0.0.0.0/8</c> (RFC 1122), <c>::/128</c> (RFC 4291).</summary>
    Unspecified,

    /// <summary>Loopback: <c>127.0.0.0/8</c> (RFC 1122), <c>::1/128</c> (RFC 4291).</summary>
    Loopback,

    /// <summary>
    /// Private networks: <c>10.0.0.0/8</c>, <c>172.16.0.0/12</c>, <c>192.168.0.0/16</c> (RFC 1918), unique local
    /// <c>fc00::/7</c> (RFC 4193) and deprecated site-local <c>fec0::/10</c> (RFC 3879).
    /// </summary>
    Private,

    /// <summary>Shared address space behind carrier-grade NAT: <c>100.64.0.0/10</c> (RFC 6598).</summary>
    CarrierGradeNat,

    /// <summary>
    /// Link-local: <c>169.254.0.0/16</c> (RFC 3927) — which includes the cloud metadata endpoint
    /// <c>169.254.169.254</c> — and <c>fe80::/10</c> (RFC 4291).
    /// </summary>
    LinkLocal,

    /// <summary>
    /// Documentation examples: <c>192.0.2.0/24</c>, <c>198.51.100.0/24</c>, <c>203.0.113.0/24</c> (RFC 5737),
    /// <c>2001:db8::/32</c> (RFC 3849), <c>3fff::/20</c> (RFC 9637).
    /// </summary>
    Documentation,

    /// <summary>Benchmarking: <c>198.18.0.0/15</c> (RFC 2544), <c>2001:2::/48</c> (RFC 5180).</summary>
    Benchmarking,

    /// <summary>Multicast: <c>224.0.0.0/4</c> (RFC 5771), <c>ff00::/8</c> (RFC 4291).</summary>
    Multicast,

    /// <summary>The limited broadcast address <c>255.255.255.255</c> (RFC 919).</summary>
    Broadcast,

    /// <summary>
    /// Reserved or protocol-specific: <c>192.0.0.0/24</c> (RFC 6890), <c>192.88.99.0/24</c> (RFC 7526),
    /// <c>240.0.0.0/4</c> (RFC 1112), <c>100::/64</c> (RFC 6666), the deprecated IPv4-compatible <c>::/96</c>, and the
    /// local-use translation prefix <c>64:ff9b:1::/48</c> (RFC 8215).
    /// </summary>
    Reserved
}
