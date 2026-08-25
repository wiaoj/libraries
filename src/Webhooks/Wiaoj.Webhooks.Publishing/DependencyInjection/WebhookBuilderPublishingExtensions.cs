using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Publishing;
using Wiaoj.Webhooks.Publishing.Internal;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods integrating 1-to-N Webhook Gateway and event fan-out capabilities into the root <see cref="IWebhookBuilder"/>.
/// </summary>
public static class WebhookBuilderPublishingExtensions {
    /// <summary>
    /// Adds 1-to-N Webhook Gateway and event fan-out broker capabilities with default in-memory store and wildcard matcher.
    /// </summary>
    /// <param name="builder">The root webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder AddPublishing(this IWebhookBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<IWebhookTopicMatcher, WildcardTopicMatcher>();
        builder.Services.TryAddSingleton<IWebhookContentFilterEvaluator, SimpleContentFilterEvaluator>();
        builder.Services.TryAddSingleton<IWebhookSubscriptionMatcher, CompositeSubscriptionMatcher>();
        builder.Services.TryAddSingleton<IWebhookSubscriptionStore, InMemoryWebhookSubscriptionStore>();
        builder.Services.TryAddSingleton<IWebhookBatchStore>(NullWebhookBatchStore.Instance);
        builder.Services.TryAddSingleton<IWebhookPublisher, WebhookPublisher>();

        return builder;
    }

    /// <summary>
    /// Adds and configures 1-to-N Webhook Gateway and event fan-out broker capabilities within the webhook pipeline using a configuration delegate.
    /// </summary>
    /// <param name="builder">The root webhook builder being configured.</param>
    /// <param name="configure">The delegate configuring gateway subscription stores, matchers, or custom routing rules.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder AddPublishing(this IWebhookBuilder builder, Action<IWebhookPublishingBuilder> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.TryAddSingleton<IWebhookTopicMatcher, WildcardTopicMatcher>();
        builder.Services.TryAddSingleton<IWebhookContentFilterEvaluator, SimpleContentFilterEvaluator>();
        builder.Services.TryAddSingleton<IWebhookSubscriptionMatcher, CompositeSubscriptionMatcher>();
        builder.Services.TryAddSingleton<IWebhookSubscriptionStore, InMemoryWebhookSubscriptionStore>();
        builder.Services.TryAddSingleton<IWebhookBatchStore>(NullWebhookBatchStore.Instance);
        builder.Services.TryAddSingleton<IWebhookPublisher, WebhookPublisher>();

        WebhookPublishingBuilder gatewayBuilder = new(builder.Services);
        configure(gatewayBuilder);

        return builder;
    }
}