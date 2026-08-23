using Wiaoj.Webhooks;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods integrating inbound webhook receiver capabilities into the root <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderInboundExtensions {
    /// <summary>
    /// Configures inbound webhook receiver policies within the main webhook builder.
    /// </summary>
    /// <param name="builder">The root webhook builder.</param>
    /// <param name="configure">The delegate configuring inbound receiver policies.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent chaining.</returns>
    public static IWebhookBuilder AddInbound(this IWebhookBuilder builder, Action<WebhookInboundBuilder>? configure = null) {
        Preca.ThrowIfNull(builder);
        builder.Services.AddInboundWebhooks(configure);
        return builder;
    }
}