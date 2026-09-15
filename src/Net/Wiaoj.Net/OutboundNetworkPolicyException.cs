namespace Wiaoj.Net;

/// <summary>
/// A connection was refused by the <see cref="OutboundNetworkPolicy"/>: its port is not allowed, or no address its host
/// resolved to is.
/// </summary>
/// <remarks>
/// Raised while connecting, so HttpClient reports it as the inner exception of an <see cref="HttpRequestException"/>.
/// The message names the host and port but not the addresses: echoing what an internal name resolves to would hand the
/// caller the map the policy exists to hide.
/// </remarks>
public sealed class OutboundNetworkPolicyException : IOException {
    /// <summary>Creates the exception for a host none of whose addresses is allowed.</summary>
    /// <param name="host">The host that was refused.</param>
    /// <param name="port">The port that was requested.</param>
    public OutboundNetworkPolicyException(string host, int port)
        : this(host, port, OutboundRefusalReason.Address) {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="host">The host that was refused.</param>
    /// <param name="port">The port that was requested.</param>
    /// <param name="reason">Why it was refused.</param>
    public OutboundNetworkPolicyException(string host, int port, OutboundRefusalReason reason)
        : base(reason == OutboundRefusalReason.Port
            ? $"The connection to '{host}:{port}' was refused: port {port} is not allowed by the outbound network policy."
            : $"The connection to '{host}:{port}' was refused: no address it resolves to is allowed by the outbound network policy.") {
        this.Host = host;
        this.Port = port;
        this.Reason = reason;
    }

    /// <summary>Gets the host that was refused.</summary>
    public string Host { get; }

    /// <summary>Gets the port that was requested.</summary>
    public int Port { get; }

    /// <summary>Gets why the connection was refused.</summary>
    public OutboundRefusalReason Reason { get; }
}