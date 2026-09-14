using System.Net;
using System.Net.Sockets;
using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Enforces an <see cref="OutboundNetworkPolicy"/> on the connections a <see cref="SocketsHttpHandler"/> opens.
/// </summary>
public static class OutboundNetworkPolicyHandlerExtensions {
    /// <summary>
    /// Makes <paramref name="handler"/> resolve each host itself and connect only to an address
    /// <paramref name="policy"/> allows.
    /// </summary>
    /// <param name="handler">The handler; not yet used to send a request.</param>
    /// <param name="policy">The policy to enforce.</param>
    /// <returns>The handler, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// The handler already has a <see cref="SocketsHttpHandler.ConnectCallback"/>, or an explicit
    /// <see cref="SocketsHttpHandler.Proxy"/>.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The check happens where the socket is opened, against the address actually connected to. Checking a URL first and
    /// connecting later lets DNS answer differently the second time (DNS rebinding); here there is no second time.
    /// </para>
    /// <para>
    /// Through a proxy, the connection opened is to the proxy, and the proxy reaches the destination — the policy would
    /// check the wrong address. So an explicit proxy is refused, and <see cref="SocketsHttpHandler.UseProxy"/> is turned
    /// off so an environment proxy (<c>HTTPS_PROXY</c>) does not silently take the connection either. Behind a proxy,
    /// the proxy is the place to enforce egress rules.
    /// </para>
    /// <para>
    /// A host resolving to both allowed and refused addresses is connected through an allowed one only.
    /// </para>
    /// </remarks>
    public static SocketsHttpHandler UseOutboundNetworkPolicy(this SocketsHttpHandler handler, OutboundNetworkPolicy policy) {
        return handler.UseOutboundNetworkPolicy(policy, DnsResolver.System);
    }

    /// <summary>
    /// Makes <paramref name="handler"/> resolve each host with <paramref name="resolver"/> and connect only to an address
    /// <paramref name="policy"/> allows.
    /// </summary>
    /// <param name="handler">The handler; not yet used to send a request.</param>
    /// <param name="policy">The policy to enforce.</param>
    /// <param name="resolver">Resolves host names; IP literals are decided without it.</param>
    /// <returns>The handler, for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// The handler already has a <see cref="SocketsHttpHandler.ConnectCallback"/>, or an explicit
    /// <see cref="SocketsHttpHandler.Proxy"/>.
    /// </exception>
    /// <remarks>See <see cref="UseOutboundNetworkPolicy(SocketsHttpHandler, OutboundNetworkPolicy)"/>.</remarks>
    public static SocketsHttpHandler UseOutboundNetworkPolicy(this SocketsHttpHandler handler, OutboundNetworkPolicy policy, DnsResolver resolver) {
        Preca.ThrowIfNull(handler);
        Preca.ThrowIfNull(policy);
        Preca.ThrowIfNull(resolver);

        if(handler.ConnectCallback is not null) {
            throw new InvalidOperationException(
                "The handler already has a ConnectCallback. The outbound network policy is enforced through its own callback " +
                "and cannot be combined with another one.");
        }

        if(handler.Proxy is not null) {
            throw new InvalidOperationException(
                "The handler has a proxy. Connections would be opened to the proxy, so the outbound network policy would check " +
                "the proxy's address instead of the destination; enforce egress rules at the proxy instead.");
        }

        handler.UseProxy = false;
        handler.ConnectCallback = (context, cancellationToken) => ConnectAsync(context.DnsEndPoint, policy, resolver, cancellationToken);
        return handler;
    }

    internal static async ValueTask<Stream> ConnectAsync(DnsEndPoint endpoint, OutboundNetworkPolicy policy, DnsResolver resolver, CancellationToken cancellationToken) {
        IPAddress[] addresses = await DnsResolver.ResolveOrParseAsync(resolver, endpoint.Host, cancellationToken).ConfigureAwait(false);

        Exception? lastError = null;

        foreach(IPAddress address in addresses) {
            if(!policy.IsAllowed(address)) {
                continue;
            }

            Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try {
                await socket.ConnectAsync(new IPEndPoint(address, endpoint.Port), cancellationToken).ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch(SocketException exception) {
                socket.Dispose();
                lastError = exception;
            }
            catch {
                socket.Dispose();
                throw;
            }
        }

        throw lastError ?? new OutboundNetworkPolicyException(endpoint.Host, endpoint.Port);
    }
}
