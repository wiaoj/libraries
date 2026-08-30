using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring standardized RFC/CNCF metadata headers on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderStandardHeadersExtensions {
    /// <summary>
    /// Injects standard metadata headers (<c>Webhook-Id</c>, <c>Webhook-Event</c>, <c>Webhook-Attempt</c>, <c>User-Agent</c>) using default options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStandardHeaders(this IWebhookBuilder builder) {
        return UseStandardHeaders(builder, new StandardHeadersOptions());
    }

    /// <summary>
    /// Injects standard metadata headers using the specified configuration options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The configuration options controlling header names and inclusions.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStandardHeaders(this IWebhookBuilder builder, StandardHeadersOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<StandardHeadersMiddleware>();
        return builder;
    }

    /// <summary>
    /// Injects standard metadata headers using a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="StandardHeadersOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseStandardHeaders(
        this IWebhookBuilder builder,
        Action<StandardHeadersOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        StandardHeadersOptions options = new();
        configure(options);

        return UseStandardHeaders(builder, options);
    }
}
