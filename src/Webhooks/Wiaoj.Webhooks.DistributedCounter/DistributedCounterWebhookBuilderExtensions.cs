using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks.DistributedCounter;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring distributed rate limiting in the Webhooks builder.
/// </summary>
public static class DistributedCounterWebhookBuilderExtensions {
    /// <summary>
    /// Configures distributed rate limiting using the specified <see cref="DistributedRateLimitingOptions"/>.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="options">The rate limiting options.</param>
    /// <returns>The webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseDistributedRateLimiting(
        this IWebhookBuilder builder,
        DistributedRateLimitingOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<DistributedRateLimitingMiddleware>();
        builder.AddMiddleware<DistributedRateLimitingMiddleware>();

        return builder;
    }

    /// <summary>
    /// Configures distributed rate limiting using maximum requests and window duration.
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <param name="maxRequestsPerWindow">The maximum allowed requests per sliding window.</param>
    /// <param name="window">The sliding window duration.</param>
    /// <returns>The webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseDistributedRateLimiting(
        this IWebhookBuilder builder,
        long maxRequestsPerWindow,
        TimeSpan window) {
        DistributedRateLimitingOptions options = new() {
            MaxRequestsPerWindow = maxRequestsPerWindow,
            Window = window
        };
        return UseDistributedRateLimiting(builder, options);
    }

    /// <summary>
    /// Configures distributed rate limiting with default settings (50 requests / 1 second).
    /// </summary>
    /// <param name="builder">The webhook builder.</param>
    /// <returns>The webhook builder instance for fluent chaining.</returns>
    public static IWebhookBuilder UseDistributedRateLimiting(this IWebhookBuilder builder) {
        return UseDistributedRateLimiting(builder, new DistributedRateLimitingOptions());
    }
}
