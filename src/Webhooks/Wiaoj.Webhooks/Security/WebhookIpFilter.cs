using System.Net;
using Wiaoj.Net;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Evaluates whether an outbound IP address is safe for webhook transmission.
/// </summary>
/// <remarks>
/// <para>
/// Superseded by <see cref="OutboundNetworkPolicy"/> in <c>Wiaoj.Net</c>, which the webhook transport now enforces, and
/// which can name exceptions such as an internal network (<see cref="WebhookSecurityOptions.NetworkPolicy"/>). This type
/// forwards to it and will be removed.
/// </para>
/// <para>
/// It refuses everything it refused before. It now also refuses addresses it used to allow: <c>3fff::/20</c>
/// (documentation, RFC 9637), <c>2001:2::/48</c> (benchmarking, RFC 5180), the deprecated IPv4-compatible <c>::/96</c>,
/// the local-use translation prefix <c>64:ff9b:1::/48</c> (RFC 8215), and IPv4-translated (SIIT) and ISATAP addresses
/// that carry a refused IPv4 address.
/// </para>
/// </remarks>
[Obsolete("Use Wiaoj.Net.OutboundNetworkPolicy (for example OutboundNetworkPolicy.PublicOnly.IsAllowed) instead. WebhookIpFilter will be removed.")]
public static class WebhookIpFilter {
    /// <summary>
    /// Evaluates whether an outbound IP address is safe for webhook transmission.
    /// </summary>
    /// <param name="ipAddress">The IP address to evaluate.</param>
    /// <param name="allowPrivateNetworks">When <see langword="true"/>, allows every address (development mode only).</param>
    /// <returns><see langword="true"/> if the IP address is allowed; otherwise, <see langword="false"/>.</returns>
    public static bool IsAllowed(IPAddress ipAddress, bool allowPrivateNetworks = false) {
        Preca.ThrowIfNull(ipAddress);
        return (allowPrivateNetworks ? OutboundNetworkPolicy.Unrestricted : OutboundNetworkPolicy.PublicOnly).IsAllowed(ipAddress);
    }
}