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
}
