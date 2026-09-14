namespace Wiaoj.Net;

/// <summary>
/// A connection was refused because every address its host resolved to is outside the <see cref="OutboundNetworkPolicy"/>.
/// </summary>
/// <remarks>
/// Raised while connecting, so HttpClient reports it as the inner exception of an <see cref="HttpRequestException"/>.
/// The message names the host and port but not the addresses: echoing what an internal name resolves to would hand the
/// caller the map the policy exists to hide.
/// </remarks>
public sealed class OutboundNetworkPolicyException : IOException {
    /// <summary>Creates the exception.</summary>
    /// <param name="host">The host that was refused.</param>
    /// <param name="port">The port that was requested.</param>
    public OutboundNetworkPolicyException(string host, int port)
        : base($"The connection to '{host}:{port}' was refused: no address it resolves to is allowed by the outbound network policy.") {
        this.Host = host;
        this.Port = port;
    }

    /// <summary>Gets the host that was refused.</summary>
    public string Host { get; }

    /// <summary>Gets the port that was requested.</summary>
    public int Port { get; }
}
