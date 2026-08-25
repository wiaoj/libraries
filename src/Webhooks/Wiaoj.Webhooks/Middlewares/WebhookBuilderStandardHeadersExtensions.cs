using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring standardized RFC/CNCF metadata headers on <see cref="IWebhookBuilder"/>.
/// </summary>
internal static partial class WebhookBuilderStandardHeadersExtensions {
    /// <summary>
    /// Injects standard metadata headers (Webhook-Id, Webhook-Event, Webhook-Attempt, User-Agent).
    /// </summary>
    public static IWebhookBuilder UseStandardHeaders(
        this IWebhookBuilder builder,
        Action<StandardHeadersOptions>? configure = null) {
        Preca.ThrowIfNull(builder);

        StandardHeadersOptions options = new();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<StandardHeadersMiddleware>();
        return builder;
    }
}