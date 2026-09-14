using System.Collections.Frozen;
using System.Net;
using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Which IP addresses outbound connections may reach — the defence against server-side request forgery.
/// </summary>
/// <remarks>
/// <para>
/// An address is decided in this order:
/// </para>
/// <list type="number">
/// <item><description><b>Blocked</b> when it, or an IPv4 address it carries, is in <see cref="BlockedNetworks"/>.</description></item>
/// <item><description><b>Allowed</b> when it is in <see cref="AllowedNetworks"/> — the exception for a known internal network.</description></item>
/// <item><description>Otherwise <b>allowed</b> only when its <see cref="IPAddressScope"/> is in <see cref="AllowedScopes"/>.</description></item>
/// </list>
/// <para>
/// Immutable: derive a policy from a preset with <c>with</c>, for example
/// <c>OutboundNetworkPolicy.PublicOnly with { AllowedNetworks = [IPNetwork.Parse("10.20.0.0/16")] }</c>.
/// </para>
/// </remarks>
public sealed record OutboundNetworkPolicy {
    private readonly IReadOnlySet<IPAddressScope> _allowedScopes = FrozenSet.ToFrozenSet([IPAddressScope.Public]);
    private readonly IReadOnlyList<IPNetwork> _allowedNetworks = [];
    private readonly IReadOnlyList<IPNetwork> _blockedNetworks = [];

    /// <summary>Gets a policy allowing only globally routable addresses.</summary>
    public static OutboundNetworkPolicy PublicOnly { get; } = new();

    /// <summary>Gets a policy allowing every address — for development, or behind an egress proxy that enforces its own.</summary>
    public static OutboundNetworkPolicy Unrestricted { get; } = new() {
        AllowedScopes = Enum.GetValues<IPAddressScope>().ToFrozenSet()
    };

    /// <summary>Gets the scopes an address may belong to. Only <see cref="IPAddressScope.Public"/> by default.</summary>
    public IReadOnlySet<IPAddressScope> AllowedScopes {
        get => this._allowedScopes;
        init {
            Preca.ThrowIfNull(value);
            this._allowedScopes = value.ToFrozenSet();
        }
    }

    /// <summary>Gets networks allowed whatever their scope, such as the internal network services talk to each other on.</summary>
    public IReadOnlyList<IPNetwork> AllowedNetworks {
        get => this._allowedNetworks;
        init {
            Preca.ThrowIfNull(value);
            this._allowedNetworks = [.. value];
        }
    }

    /// <summary>Gets networks refused whatever else allows them. Checked first.</summary>
    public IReadOnlyList<IPNetwork> BlockedNetworks {
        get => this._blockedNetworks;
        init {
            Preca.ThrowIfNull(value);
            this._blockedNetworks = [.. value];
        }
    }

    /// <summary>
    /// Returns whether a connection to <paramref name="address"/> is allowed.
    /// </summary>
    /// <param name="address">The address about to be connected to.</param>
    /// <returns><see langword="true"/> when the policy allows it.</returns>
    public bool IsAllowed(IPAddress address) {
        Preca.ThrowIfNull(address);

        // IPNetwork already matches an IPv4-mapped address against an IPv4 network. A 6to4 or NAT64 address is not
        // mapped, so the IPv4 address it carries is checked against the blocked networks as well.
        IPAddressClassifier.TryGetIPv4(address, out IPAddress? carried);

        foreach(IPNetwork blocked in this._blockedNetworks) {
            if(blocked.Contains(address) || (carried is not null && blocked.Contains(carried))) {
                return false;
            }
        }

        foreach(IPNetwork allowed in this._allowedNetworks) {
            if(allowed.Contains(address)) {
                return true;
            }
        }

        return this._allowedScopes.Contains(IPAddressClassifier.Classify(address));
    }

    /// <summary>
    /// Resolves <paramref name="host"/> and returns whether it may be connected to — for validating a URL when it is
    /// registered, before any request is made.
    /// </summary>
    /// <param name="host">A host name or IP literal (with or without IPv6 brackets).</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>
    /// <see cref="OutboundHostStatus.Allowed"/> when at least one resolved address is allowed — the same rule the
    /// connection-time check applies, which connects only through an allowed address.
    /// </returns>
    /// <remarks>
    /// This is an early answer, not the protection: DNS can answer differently by the time a request is sent. Enforce
    /// the policy at connection time as well, with <see cref="OutboundNetworkPolicyHandlerExtensions.UseOutboundNetworkPolicy"/>.
    /// </remarks>
    public async ValueTask<OutboundHostCheck> CheckHostAsync(string host, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(host);

        IPAddress[] addresses;
        if(IPAddress.TryParse(host, out IPAddress? literal)) {
            addresses = [literal];
        }
        else {
            try {
                addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch(Exception exception) when(exception is System.Net.Sockets.SocketException or ArgumentException) {
                return new OutboundHostCheck(host, OutboundHostStatus.Unresolvable, exception);
            }
        }

        if(addresses.Length == 0) {
            return new OutboundHostCheck(host, OutboundHostStatus.Unresolvable);
        }

        return new OutboundHostCheck(host, addresses.Any(this.IsAllowed) ? OutboundHostStatus.Allowed : OutboundHostStatus.Refused);
    }

    /// <summary>
    /// Checks the host of <paramref name="url"/>; see <see cref="CheckHostAsync(string, CancellationToken)"/>.
    /// </summary>
    /// <param name="url">An absolute URL.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The decision about the URL's host.</returns>
    public ValueTask<OutboundHostCheck> CheckHostAsync(Uri url, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(url);
        Preca.ThrowIfFalse(url.IsAbsoluteUri, static () => new ArgumentException("The URL must be absolute.", nameof(url)));

        // IdnHost is the punycode form DNS resolves; an IPv6 literal keeps its brackets, which IPAddress.TryParse accepts.
        return this.CheckHostAsync(url.IdnHost, cancellationToken);
    }
}
