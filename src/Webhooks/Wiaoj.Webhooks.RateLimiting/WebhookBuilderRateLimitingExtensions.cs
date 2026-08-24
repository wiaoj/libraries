using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting;
using Wiaoj.Webhooks.RateLimiting;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring rate limiting in the Webhooks builder.
/// </summary>
public static class WebhookBuilderRateLimitingExtensions {
    /// <summary>
    /// Enables rate limiting for outbound webhook deliveries using an <see cref="IRateLimitAlgorithm"/>.
    /// </summary>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        Action<WebhookRateLimitingOptions>? configure = null) {
        Preca.ThrowIfNull(builder);

        WebhookRateLimitingOptions options = new();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<WebhookRateLimitingMiddleware>();
        builder.AddMiddleware<WebhookRateLimitingMiddleware>();

        return builder;
    }
}