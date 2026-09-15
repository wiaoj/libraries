namespace Wiaoj.Net;

/// <summary>What an <see cref="OutboundNetworkPolicy"/> decided about a host.</summary>
public enum OutboundHostStatus {
    /// <summary>The host resolves to at least one address the policy allows.</summary>
    Allowed,

    /// <summary>Every address the host resolves to is outside the policy.</summary>
    Refused,

    /// <summary>The host could not be resolved, so nothing could be decided.</summary>
    Unresolvable
}

/// <summary>Why an <see cref="OutboundNetworkPolicy"/> refused a destination.</summary>
public enum OutboundRefusalReason {
    /// <summary>No address the host resolves to is allowed: each is outside the allowed scopes and networks.</summary>
    Address,

    /// <summary>The port is not allowed, whatever the address.</summary>
    Port,

    /// <summary>No address the host resolves to is allowed, and at least one of them is in a blocked network.</summary>
    BlockedNetwork
}

/// <summary>
/// The outcome of checking a host against an <see cref="OutboundNetworkPolicy"/> before connecting to it.
/// </summary>
/// <remarks>
/// A refused or unresolvable host is an ordinary outcome of checking a URL someone supplied, not a failure of the code
/// checking it, so it is returned rather than thrown. The resolved addresses are deliberately not included: a caller
/// that echoes them would reveal what internal names resolve to.
/// </remarks>
/// <param name="Host">The host that was checked.</param>
/// <param name="Status">The decision.</param>
/// <param name="ResolutionError">The resolver's error when <see cref="Status"/> is <see cref="OutboundHostStatus.Unresolvable"/>.</param>
public readonly record struct OutboundHostCheck(string Host, OutboundHostStatus Status, Exception? ResolutionError = null) {
    /// <summary>Gets whether the host may be connected to.</summary>
    public bool IsAllowed => this.Status == OutboundHostStatus.Allowed;

    /// <summary>Gets why the destination was refused when <see cref="Status"/> is <see cref="OutboundHostStatus.Refused"/>.</summary>
    public OutboundRefusalReason? RefusalReason { get; init; }

    /// <summary>The scope of the address that decided a refusal, for the refusal metric; never shown to callers.</summary>
    internal IPAddressScope? RefusedScope { get; init; }
}
