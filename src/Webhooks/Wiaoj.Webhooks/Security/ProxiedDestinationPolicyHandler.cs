using System.Net;
using Microsoft.Extensions.Options;
using Wiaoj.Net;

namespace Wiaoj.Webhooks.Security;

/// <summary>
/// Checks a delivery's destination before it is sent through a proxy, where the connection-time policy cannot see it.
/// </summary>
/// <remarks>
/// <para>
/// Without a proxy this handler does nothing: <see cref="OutboundNetworkPolicyHandlerExtensions.UseOutboundNetworkPolicy"/>
/// checks the address actually connected to. Through a proxy the socket reaches the proxy instead, so the destination is
/// checked here:
/// </para>
/// <list type="bullet">
/// <item><description>An IP-literal destination is decided exactly — no DNS is involved.</description></item>
/// <item><description>A host name is resolved and refused when it resolves only to refused addresses. Best effort: the
/// proxy resolves it again, possibly differently. A name that does not resolve here is let through, since the proxy may
/// resolve names this host cannot.</description></item>
/// </list>
/// <para>
/// A refusal is thrown as <see cref="OutboundNetworkPolicyException"/>, which the deliverer reports as a permanent
/// <see cref="PermanentFailureReason.InvalidDestination"/> failure, exactly as a connection-time refusal.
/// </para>
/// </remarks>
internal sealed class ProxiedDestinationPolicyHandler(IOptions<WebhookSecurityOptions> options) : DelegatingHandler {
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        WebhookSecurityOptions security = options.Value;

        if(security.Proxy is not null && !security.AllowPrivateNetworks && request.RequestUri is { IsAbsoluteUri: true } url) {
            OutboundHostCheck check = await security.EffectiveNetworkPolicy.CheckHostAsync(url, cancellationToken).ConfigureAwait(false);

            if(check.Status == OutboundHostStatus.Refused) {
                throw new OutboundNetworkPolicyException(url.Host, url.Port);
            }
        }

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
