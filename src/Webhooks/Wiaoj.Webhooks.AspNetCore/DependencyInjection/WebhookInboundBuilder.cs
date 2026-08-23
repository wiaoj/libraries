using Wiaoj.Preconditions;
using Wiaoj.Webhooks.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Fluent builder for registering named inbound webhook policies.
/// </summary>
public sealed class WebhookInboundBuilder {
    /// <summary>Gets the underlying service collection.</summary>
    public IServiceCollection Services { get; }

    /// <summary>Initializes a new builder instance.</summary>
    public WebhookInboundBuilder(IServiceCollection services) {
        this.Services = services;
    }

    /// <summary>
    /// Adds or configures a named inbound webhook policy (e.g. <c>"Stripe"</c>, <c>"Apple"</c>).
    /// </summary>
    public WebhookInboundBuilder AddPolicy(string name, Action<WebhookReceiverPolicy> configure) {
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNull(configure);

        this.Services.Configure<WebhookInboundOptions>(options => {
            if(!options.Policies.TryGetValue(name, out WebhookReceiverPolicy? policy)) {
                policy = new WebhookReceiverPolicy { Name = name };
                options.Policies[name] = policy;
            }
            configure(policy);
        });

        return this;
    }
}