using Microsoft.Extensions.DependencyInjection;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring global node options and instance identity on <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderOptionsExtensions {
    /// <summary>
    /// Explicitly configures the global instance/pod identifier (e.g. from Kubernetes <c>HOSTNAME</c> or container ID).
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="instanceId">The custom instance identifier.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="instanceId"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public static IWebhookBuilder WithInstanceId(this IWebhookBuilder builder, string instanceId) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(instanceId);

        builder.Services.Configure<WebhookOptions>(options => {
            options.InstanceId = instanceId;
        });

        return builder;
    }

    /// <summary>
    /// Configures global webhook engine options using a delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The configuration delegate.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder Configure(this IWebhookBuilder builder, Action<WebhookOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        return builder;
    }
}