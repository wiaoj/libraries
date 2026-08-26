using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Preconditions;
using Wiaoj.RateLimiting;
using Wiaoj.Webhooks.RateLimiting;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring rate limiting in the webhook delivery pipeline.
/// </summary>
public static class WebhookBuilderRateLimitingExtensions {
    /// <summary>
    /// Enables rate limiting for outbound webhook deliveries using the default rate limiting policy.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseRateLimiting(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);

        WebhookRateLimitingOptions options = new();
        builder.Services.TryAddSingleton(options);
        builder.AddMiddleware<WebhookRateLimitingMiddleware>();

        return builder;
    }

    /// <summary>
    /// Enables rate limiting for outbound webhook deliveries using the default rate limiting policy with custom options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="WebhookRateLimitingOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        Action<WebhookRateLimitingOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        WebhookRateLimitingOptions options = new();
        configure(options);

        builder.Services.TryAddSingleton(options);
        builder.AddMiddleware<WebhookRateLimitingMiddleware>();

        return builder;
    }

    /// <summary>
    /// Enables rate limiting for outbound webhook deliveries using a named rate limiting policy.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="policyName">The name of the rate limiting policy to apply.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is null, empty, or whitespace.</exception>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        string policyName) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);

        WebhookRateLimitingOptions options = new() { PolicyName = policyName };
        builder.Services.TryAddSingleton(options);
        builder.AddMiddleware<WebhookRateLimitingMiddleware>();

        return builder;
    }

    /// <summary>
    /// Enables rate limiting for outbound webhook deliveries using a named rate limiting policy with custom options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="policyName">The name of the rate limiting policy to apply.</param>
    /// <param name="configure">The delegate used to configure <see cref="WebhookRateLimitingOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is null, empty, or whitespace.</exception>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        string policyName,
        Action<WebhookRateLimitingOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configure);

        WebhookRateLimitingOptions options = new() { PolicyName = policyName };
        configure(options);

        builder.Services.TryAddSingleton(options);
        builder.AddMiddleware<WebhookRateLimitingMiddleware>();

        return builder;
    }

    /// <summary>
    /// Configures and enables an inline rate limiting policy directly within the webhook builder.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="configurePolicy">The delegate used to configure the rate limiting policy.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configurePolicy"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is null, empty, or whitespace.</exception>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        string policyName,
        Action<IRateLimitPolicyBuilder> configurePolicy) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configurePolicy);

        builder.Services.AddWiaojRateLimiting(rl => rl.AddPolicy(policyName, configurePolicy));
        return builder.UseRateLimiting(policyName);
    }

    /// <summary>
    /// Configures and enables an inline rate limiting policy directly within the webhook builder with custom options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="policyName">The unique policy name.</param>
    /// <param name="configurePolicy">The delegate used to configure the rate limiting policy.</param>
    /// <param name="configureOptions">The delegate used to configure <see cref="WebhookRateLimitingOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="configurePolicy"/>, or <paramref name="configureOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="policyName"/> is null, empty, or whitespace.</exception>
    public static IWebhookBuilder UseRateLimiting(
        this IWebhookBuilder builder,
        string policyName,
        Action<IRateLimitPolicyBuilder> configurePolicy,
        Action<WebhookRateLimitingOptions> configureOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(policyName);
        Preca.ThrowIfNull(configurePolicy);
        Preca.ThrowIfNull(configureOptions);

        builder.Services.AddWiaojRateLimiting(rl => rl.AddPolicy(policyName, configurePolicy));
        return builder.UseRateLimiting(policyName, configureOptions);
    }
}