using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.LoopDetection;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring loop detection and hop limit policies on <see cref="IWebhookBuilder"/>.
/// </summary>
/// <remarks>
/// Loop detection is on by default: <c>AddWiaojWebhooks</c> registers <see cref="LoopDetectionMiddleware"/> as the first
/// middleware of the delivery pipeline. These methods only change its options; calling them never adds the middleware a
/// second time. Use <see cref="DisableLoopDetection"/> to opt out.
/// </remarks>
public static partial class WebhookBuilderLoopDetectionExtensions {
    /// <summary>
    /// Keeps loop detection on with its default options. Loop detection is already on by default; this method exists so the
    /// intent can be stated explicitly, and it re-enables loop detection after <see cref="DisableLoopDetection"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder) {
        return UseLoopDetection(builder, static options => options.Enabled = true);
    }

    /// <summary>
    /// Enables loop detection with the specified maximum allowable hop count.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="maxHops">The maximum allowable hop count before delivery is intercepted.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maxHops"/> is non-positive.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder, int maxHops) {
        Preca.ThrowIfLessThanOrEqualTo(maxHops, 0);
        return UseLoopDetection(builder, options => {
            options.Enabled = true;
            options.MaxHops = maxHops;
        });
    }

    /// <summary>
    /// Replaces the loop detection options with a copy of <paramref name="options"/>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The loop detection configuration options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseLoopDetection(this IWebhookBuilder builder, LoopDetectionOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        return UseLoopDetection(builder, options.CopyTo);
    }

    /// <summary>
    /// Configures loop detection using a configuration delegate.
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

        AddLoopDetection(builder);
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Turns outbound loop detection off: deliveries are no longer checked against the hop limit or the causal chain, and
    /// no hop count or causal chain headers are sent. Inbound receivers are configured separately, through
    /// <c>WebhookReceiverPolicy.EnableLoopDetection</c>.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder DisableLoopDetection(this IWebhookBuilder builder) {
        return UseLoopDetection(builder, static options => options.Enabled = false);
    }

    /// <summary>
    /// Registers the loop detection options and middleware once per service collection.
    /// </summary>
    internal static void AddLoopDetection(IWebhookBuilder builder) {
        builder.Services.AddOptions<LoopDetectionOptions>();
        builder.Services.TryAddSingleton(static sp => sp.GetRequiredService<IOptions<LoopDetectionOptions>>().Value);

        // The middleware stamps one hop per delivery: registering it twice would count every hop twice, and the second
        // instance would find its own service in the chain the first one just wrote.
        if(builder.Services.Any(static descriptor => descriptor.ServiceType == typeof(LoopDetectionMiddleware))) {
            return;
        }

        builder.AddMiddleware<LoopDetectionMiddleware>();
    }
}
