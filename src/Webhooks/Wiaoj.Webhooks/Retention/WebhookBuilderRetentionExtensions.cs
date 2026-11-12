using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring background retention pruning on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderRetentionExtensions {
    /// <summary>
    /// Enables the background retention pruning service with default configuration
    /// (1-hour interval, 7-day delivered retention, 30-day dead-letter retention).
    /// </summary>
    public static IWebhookBuilder UseRetentionPruning(this IWebhookBuilder builder) {
        return UseRetentionPruning(builder, new WebhookRetentionOptions());
    }

    /// <summary>
    /// Enables the background retention pruning service with specified options.
    /// </summary>
    public static IWebhookBuilder UseRetentionPruning(this IWebhookBuilder builder, WebhookRetentionOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddSingleton(Options.Create(options));
        builder.Services.AddHostedService<WebhookRetentionCleanerService>();
        return builder;
    }

    /// <summary>
    /// Enables the background retention pruning service with a configuration delegate.
    /// </summary>
    public static IWebhookBuilder UseRetentionPruning(this IWebhookBuilder builder, Action<WebhookRetentionOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        WebhookRetentionOptions options = new();
        configure(options);
        return UseRetentionPruning(builder, options);
    }
}
