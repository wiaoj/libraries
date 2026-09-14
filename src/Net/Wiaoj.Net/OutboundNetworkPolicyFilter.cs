using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Wiaoj.Net;

/// <summary>The policies registered per HttpClient name.</summary>
internal sealed class OutboundNetworkPolicyRegistry {
    public Dictionary<string, OutboundNetworkPolicy> Policies { get; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Applies a client's policy to its primary handler after every configuration action has run.
/// </summary>
/// <remarks>
/// A builder filter wraps the client's configuration, so it sees the primary handler as it finally is. A configuration
/// action registered through the client builder would run in registration order, and a later
/// <c>ConfigurePrimaryHttpMessageHandler</c> would replace the protected handler.
/// </remarks>
internal sealed class OutboundNetworkPolicyFilter(IOptions<OutboundNetworkPolicyRegistry> registry) : IHttpMessageHandlerBuilderFilter {
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next) {
        return builder => {
            next(builder);

            if(builder.Name is null || !registry.Value.Policies.TryGetValue(builder.Name, out OutboundNetworkPolicy? policy)) {
                return;
            }

            if(builder.PrimaryHandler is not SocketsHttpHandler sockets) {
                throw new InvalidOperationException(
                    $"The HttpClient '{builder.Name}' has an outbound network policy, but its primary handler is " +
                    $"{builder.PrimaryHandler?.GetType().Name ?? "null"}. The policy is enforced by SocketsHttpHandler; configure " +
                    "one as the primary handler.");
            }

            sockets.UseOutboundNetworkPolicy(policy);
        };
    }
}
