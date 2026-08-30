using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Webhooks.LoopDetection;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring loop detection and hop limit policies on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderLoopDetectionExtensions {
    /// <summary>
    /// Enables automatic loop detection, causal chain cycle prevention, and maximum hop limit enforcement using default options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder) {
        return UseLoopDetection(builder, new LoopDetectionOptions());
    }

    /// <summary>
    /// Enables automatic loop detection with the specified maximum allowable hop count.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="maxHops">The maximum allowable hop count before delivery is intercepted.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxHops"/> is non-positive.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder, int maxHops) {
        Preca.ThrowIfLessThanOrEqualTo(maxHops, 0);
        return UseLoopDetection(builder, new LoopDetectionOptions { MaxHops = maxHops });
    }

    /// <summary>
    /// Enables automatic loop detection using the specified configuration options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The loop detection configuration options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder, LoopDetectionOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        builder.Services.AddSingleton(options);
        builder.AddMiddleware<LoopDetectionMiddleware>();
        return builder;
    }

    /// <summary>
    /// Enables automatic loop detection and configures options using a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="LoopDetectionOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseLoopDetection(
        this IWebhookBuilder builder,
        Action<LoopDetectionOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        LoopDetectionOptions options = new();
        configure(options);

        return UseLoopDetection(builder, options);
    }
}