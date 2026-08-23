using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring background stale in-flight job recovery on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderRecoveryExtensions {
    /// <summary>
    /// Enables the background stale in-flight job recovery service with default configuration (30-second interval).
    /// </summary>
    public static IWebhookBuilder UseStaleJobRecovery(this IWebhookBuilder builder) {
        return UseStaleJobRecovery(builder, new WebhookRecoveryOptions());
    }

    /// <summary>
    /// Enables the background stale in-flight job recovery service with specified options.
    /// </summary>
    public static IWebhookBuilder UseStaleJobRecovery(this IWebhookBuilder builder, WebhookRecoveryOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddSingleton(Options.Create(options));
        builder.Services.AddHostedService<StaleJobRecoveryService>();
        return builder;
    }

    /// <summary>
    /// Enables the background stale in-flight job recovery service with a configuration delegate.
    /// </summary>
    public static IWebhookBuilder UseStaleJobRecovery(this IWebhookBuilder builder, Action<WebhookRecoveryOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        WebhookRecoveryOptions options = new();
        configure(options);
        return UseStaleJobRecovery(builder, options);
    }
}