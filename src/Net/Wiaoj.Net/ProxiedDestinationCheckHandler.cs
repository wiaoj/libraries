using Wiaoj.Preconditions;

namespace Wiaoj.Net;

/// <summary>
/// Checks each request's destination against an <see cref="OutboundNetworkPolicy"/> before it is sent through a proxy,
/// where the connection-time policy cannot see it.
/// </summary>
/// <remarks>
/// <para>
/// Without a proxy, use <see cref="OutboundNetworkPolicyHandlerExtensions.UseOutboundNetworkPolicy(SocketsHttpHandler, OutboundNetworkPolicy, DnsResolver)"/>
/// instead: it checks the address actually connected to. Through a proxy the socket reaches the proxy, so the destination
/// is checked here, from the request URL:
/// </para>
/// <list type="bullet">
/// <item><description>A port the policy does not allow is refused without any lookup.</description></item>
/// <item><description>An IP-literal destination is decided exactly — no DNS is involved.</description></item>
/// <item><description>A host name is resolved and refused when it resolves only to refused addresses. <b>Best effort:</b>
/// the proxy resolves it again, possibly differently, so the proxy must still enforce egress rules. A name that does not
/// resolve here is let through, since the proxy may resolve names this host cannot.</description></item>
/// </list>
/// <para>
/// A refusal throws <see cref="OutboundNetworkPolicyException"/> and is counted in <c>wiaoj.net.outbound.refused</c> with
/// <c>stage=request</c>.
/// </para>
/// </remarks>
public sealed class ProxiedDestinationCheckHandler : DelegatingHandler {
    private readonly OutboundNetworkPolicy _policy;
    private readonly DnsResolver _resolver;

    /// <summary>Creates the handler, resolving host names with <see cref="DnsResolver.System"/>.</summary>
    /// <param name="policy">The policy destinations are checked against.</param>
    public ProxiedDestinationCheckHandler(OutboundNetworkPolicy policy)
        : this(policy, DnsResolver.System) {
    }

    /// <summary>Creates the handler.</summary>
    /// <param name="policy">The policy destinations are checked against.</param>
    /// <param name="resolver">Resolves host names; IP literals are decided without it.</param>
    public ProxiedDestinationCheckHandler(OutboundNetworkPolicy policy, DnsResolver resolver) {
        Preca.ThrowIfNull(policy);
        Preca.ThrowIfNull(resolver);

        this._policy = policy;
        this._resolver = resolver;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if(request.RequestUri is { IsAbsoluteUri: true } url) {
            OutboundHostCheck check = await this._policy.CheckHostAsync(url, this._resolver, cancellationToken).ConfigureAwait(false);

            if(check.Status == OutboundHostStatus.Refused) {
                OutboundRefusalReason reason = check.RefusalReason ?? OutboundRefusalReason.Address;
                OutboundNetworkMeter.RecordRefused(OutboundNetworkMeter.RequestStage, reason, check.RefusedScope);
                throw new OutboundNetworkPolicyException(url.Host, url.Port, reason);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}