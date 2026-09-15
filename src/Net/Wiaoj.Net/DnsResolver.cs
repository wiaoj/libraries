using System.Net;
using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Resolves host names to IP addresses for outbound connections.
/// </summary>
/// <remarks>
/// <para>
/// An abstract class rather than an interface, following <see cref="TimeProvider"/>: <see cref="System"/> is the default,
/// and members added later (an address family filter, TTLs) can be <see langword="virtual"/> without breaking existing
/// implementations. Register a replacement in the container — a corporate resolver, or a fake in tests — and
/// <c>AddOutboundNetworkPolicy</c> uses it.
/// </para>
/// <para>
/// IP literals never reach a resolver: the policy code decides them itself.
/// </para>
/// </remarks>
public abstract class DnsResolver {
    /// <summary>Gets the resolver backed by the operating system, through <see cref="Dns.GetHostAddressesAsync(string, CancellationToken)"/>.</summary>
    public static DnsResolver System { get; } = new SystemDnsResolver();

    /// <summary>
    /// Resolves <paramref name="host"/> to its addresses.
    /// </summary>
    /// <param name="host">A host name; never an IP literal.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The addresses, in the order connections should be attempted.</returns>
    /// <exception cref="System.Net.Sockets.SocketException">The host does not exist or cannot be resolved.</exception>
    /// <remarks>
    /// Report a host that cannot be resolved with <see cref="System.Net.Sockets.SocketException"/>, as the system resolver
    /// does: <see cref="OutboundNetworkPolicy.CheckHostAsync(string, DnsResolver, CancellationToken)"/> reports it as
    /// <see cref="OutboundHostStatus.Unresolvable"/>.
    /// </remarks>
    public abstract ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);

    /// <summary>Returns an IP literal as itself, and resolves anything else with <paramref name="resolver"/>.</summary>
    internal static ValueTask<IPAddress[]> ResolveOrParseAsync(DnsResolver resolver, string host, CancellationToken cancellationToken) {
        return IPAddress.TryParse(host, out IPAddress? literal)
            ? ValueTask.FromResult<IPAddress[]>([literal])
            : resolver.ResolveAsync(host, cancellationToken);
    }

    private sealed class SystemDnsResolver : DnsResolver {
        public override async ValueTask<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) {
            Preca.ThrowIfNullOrWhiteSpace(host);
            return await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
    }
}
