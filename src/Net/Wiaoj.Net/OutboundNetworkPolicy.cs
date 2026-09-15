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
/// <item><description><b>Refused</b> when its port is in <see cref="BlockedPorts"/>, or <see cref="AllowedPorts"/> is set and does not contain it.</description></item>
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
    private readonly IReadOnlySet<int>? _allowedPorts;
    private readonly IReadOnlySet<int> _blockedPorts = FrozenSet<int>.Empty;

    /// <summary>Gets a policy allowing only globally routable addresses.</summary>
    public static OutboundNetworkPolicy PublicOnly { get; } = new();

    /// <summary>
    /// Gets a policy allowing only globally routable addresses on the web ports, 80 and 443.
    /// </summary>
    /// <remarks>
    /// For clients that only ever speak HTTP(S) to public sites, such as metadata discovery. It also refuses public hosts
    /// on other ports, so a receiver on 8443 needs <see cref="PublicOnly"/> with its own <see cref="AllowedPorts"/>.
    /// </remarks>
    public static OutboundNetworkPolicy WebOnly { get; } = new() {
        AllowedPorts = FrozenSet.ToFrozenSet([80, 443])
    };

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
    /// Gets the only ports connections may use, or <see langword="null"/> (the default) for any port.
    /// </summary>
    /// <remarks>
    /// Internal services are often reached by port as much as by address: Redis on 6379, the Docker API on 2375, SSH on 22.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">A port is outside 1–65535.</exception>
    public IReadOnlySet<int>? AllowedPorts {
        get => this._allowedPorts;
        init => this._allowedPorts = value is null ? null : ValidatePorts(value);
    }

    /// <summary>Gets ports refused whatever else allows them. Empty by default.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A port is outside 1–65535.</exception>
    public IReadOnlySet<int> BlockedPorts {
        get => this._blockedPorts;
        init {
            Preca.ThrowIfNull(value);
            this._blockedPorts = ValidatePorts(value);
        }
    }

    /// <summary>
    /// Returns whether a connection to <paramref name="address"/> on <paramref name="port"/> is allowed.
    /// </summary>
    /// <param name="address">The address about to be connected to.</param>
    /// <param name="port">The port about to be connected to, 1–65535.</param>
    /// <returns><see langword="true"/> when the policy allows both the port and the address.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> is outside 1–65535.</exception>
    public bool IsAllowed(IPAddress address, int port) {
        Preca.ThrowIfNull(address);
        Preca.ThrowIfOutOfRange(port, IPEndPoint.MinPort + 1, IPEndPoint.MaxPort);

        return this.IsPortAllowed(port) && this.IsAllowed(address);
    }

    /// <summary>
    /// Returns whether a connection to <paramref name="address"/> is allowed, whatever the port; port rules are applied by
    /// <see cref="IsAllowed(IPAddress, int)"/>.
    /// </summary>
    /// <param name="address">The address about to be connected to.</param>
    /// <returns><see langword="true"/> when the policy allows it.</returns>
    public bool IsAllowed(IPAddress address) {
        Preca.ThrowIfNull(address);

        // IPNetwork already matches an IPv4-mapped address against an IPv4 network. Every other IPv4 address the address
        // carries — tunnelled or translated — is checked against the blocked networks too.
        Span<IPAddress?> carried = [null, null];
        int carriedCount = IPAddressClassifier.GetCarriedIPv4(address, carried);

        foreach(IPNetwork blocked in this._blockedNetworks) {
            if(blocked.Contains(address)) {
                return false;
            }

            for(int i = 0; i < carriedCount; i++) {
                if(blocked.Contains(carried[i]!)) {
                    return false;
                }
            }
        }

        // An allowed network is an exception for a destination. A translated address (NAT64, SIIT) reaches the IPv4 host it
        // carries, so it is allowed with it; a tunnel's carried address (6to4, Teredo, ISATAP) is an endpoint, not the host
        // reached, so it grants nothing.
        IPAddressClassifier.TryGetTranslatedIPv4(address, out IPAddress? translated);

        foreach(IPNetwork allowed in this._allowedNetworks) {
            if(allowed.Contains(address) || (translated is not null && allowed.Contains(translated))) {
                return true;
            }
        }

        return this._allowedScopes.Contains(IPAddressClassifier.Classify(address));
    }

    /// <summary>Returns whether <paramref name="port"/> passes the port rules; blocked ports win.</summary>
    internal bool IsPortAllowed(int port) {
        return !this._blockedPorts.Contains(port) && (this._allowedPorts is null || this._allowedPorts.Contains(port));
    }

    private static FrozenSet<int> ValidatePorts(IEnumerable<int> ports) {
        FrozenSet<int> frozen = ports.ToFrozenSet();
        foreach(int port in frozen) {
            Preca.ThrowIfOutOfRange(port, IPEndPoint.MinPort + 1, IPEndPoint.MaxPort, "value");
        }

        return frozen;
    }

    /// <summary>
    /// Resolves <paramref name="host"/> with the system resolver and returns whether it may be connected to; see
    /// <see cref="CheckHostAsync(string, DnsResolver, CancellationToken)"/>.
    /// </summary>
    /// <param name="host">A host name or IP literal (with or without IPv6 brackets).</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The decision about the host.</returns>
    public ValueTask<OutboundHostCheck> CheckHostAsync(string host, CancellationToken cancellationToken = default) {
        return this.CheckHostAsync(host, DnsResolver.System, cancellationToken);
    }

    /// <summary>
    /// Resolves <paramref name="host"/> and returns whether it may be connected to — for validating a URL when it is
    /// registered, before any request is made. Port rules need a port; the <see cref="Uri"/> overloads apply them.
    /// </summary>
    /// <param name="host">A host name or IP literal (with or without IPv6 brackets).</param>
    /// <param name="resolver">Resolves host names; an IP literal is decided without it.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>
    /// <see cref="OutboundHostStatus.Allowed"/> when at least one resolved address is allowed — the same rule the
    /// connection-time check applies, which connects only through an allowed address.
    /// </returns>
    /// <remarks>
    /// This is an early answer, not the protection: DNS can answer differently by the time a request is sent. Enforce
    /// the policy at connection time as well, with <see cref="OutboundNetworkPolicyHandlerExtensions.UseOutboundNetworkPolicy(SocketsHttpHandler, OutboundNetworkPolicy, DnsResolver)"/>.
    /// </remarks>
    public async ValueTask<OutboundHostCheck> CheckHostAsync(string host, DnsResolver resolver, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(host);
        Preca.ThrowIfNull(resolver);

        IPAddress[] addresses;
        try {
            addresses = await DnsResolver.ResolveOrParseAsync(resolver, host, cancellationToken).ConfigureAwait(false);
        }
        catch(Exception exception) when(exception is System.Net.Sockets.SocketException or ArgumentException) {
            return new OutboundHostCheck(host, OutboundHostStatus.Unresolvable, exception);
        }

        if(addresses.Length == 0) {
            return new OutboundHostCheck(host, OutboundHostStatus.Unresolvable);
        }

        return addresses.Any(this.IsAllowed)
            ? new OutboundHostCheck(host, OutboundHostStatus.Allowed)
            : new OutboundHostCheck(host, OutboundHostStatus.Refused) { RefusalReason = OutboundRefusalReason.Address };
    }

    /// <summary>
    /// Checks the host of <paramref name="url"/> with the system resolver; see
    /// <see cref="CheckHostAsync(string, DnsResolver, CancellationToken)"/>.
    /// </summary>
    /// <param name="url">An absolute URL.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>The decision about the URL's host.</returns>
    public ValueTask<OutboundHostCheck> CheckHostAsync(Uri url, CancellationToken cancellationToken = default) {
        return this.CheckHostAsync(url, DnsResolver.System, cancellationToken);
    }

    /// <summary>
    /// Checks the port and host of <paramref name="url"/>; see <see cref="CheckHostAsync(string, DnsResolver, CancellationToken)"/>.
    /// </summary>
    /// <param name="url">An absolute URL.</param>
    /// <param name="resolver">Resolves host names; an IP literal is decided without it.</param>
    /// <param name="cancellationToken">Cancels the resolution.</param>
    /// <returns>
    /// The decision about the URL. A refused port is <see cref="OutboundHostStatus.Refused"/> with
    /// <see cref="OutboundRefusalReason.Port"/>, decided before the host is resolved.
    /// </returns>
    /// <remarks>
    /// The port is the URL's explicit port, or its scheme's default (80 for http, 443 for https). A URL whose scheme has no
    /// default port and names none is refused when <see cref="AllowedPorts"/> is set, since the port it will use is unknown.
    /// </remarks>
    public async ValueTask<OutboundHostCheck> CheckHostAsync(Uri url, DnsResolver resolver, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(url);
        Preca.ThrowIfFalse(url.IsAbsoluteUri, static () => new ArgumentException("The URL must be absolute.", nameof(url)));

        bool portAllowed = url.Port > 0 ? this.IsPortAllowed(url.Port) : this._allowedPorts is null;
        if(!portAllowed) {
            return new OutboundHostCheck(url.IdnHost, OutboundHostStatus.Refused) { RefusalReason = OutboundRefusalReason.Port };
        }

        // IdnHost is the punycode form DNS resolves; an IPv6 literal keeps its brackets, which IPAddress.TryParse accepts.
        return await this.CheckHostAsync(url.IdnHost, resolver, cancellationToken).ConfigureAwait(false);
    }
}