using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Publishing.Internal;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks.Publishing;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods on <see cref="IWebhookPublishingBuilder"/> for configuring stores and matchers.
/// </summary>
public static class WebhookPublishingBuilderExtensions {
    /// <summary>
    /// Configures the default thread-safe in-memory subscription store.
    /// </summary>
    /// <param name="builder">The gateway builder being configured.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseInMemoryStore(this IWebhookPublishingBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSubscriptionStore>();
        builder.Services.AddSingleton<IWebhookSubscriptionStore, InMemoryWebhookSubscriptionStore>();
        return builder;
    }

    /// <summary>
    /// Configures a custom subscription store type (e.g. EF Core, Redis, Postgres).
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IWebhookSubscriptionStore"/>.</typeparam>
    /// <param name="builder">The gateway builder being configured.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseStore<TStore>(this IWebhookPublishingBuilder builder)
        where TStore : class, IWebhookSubscriptionStore {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSubscriptionStore>();
        builder.Services.AddSingleton<IWebhookSubscriptionStore, TStore>();
        return builder;
    }

    /// <summary>
    /// Configures an explicit singleton instance of the subscription store.
    /// </summary>
    /// <param name="builder">The gateway builder being configured.</param>
    /// <param name="store">The subscription store instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseStore(this IWebhookPublishingBuilder builder, IWebhookSubscriptionStore store) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(store);
        builder.Services.RemoveAll<IWebhookSubscriptionStore>();
        builder.Services.AddSingleton(store);
        return builder;
    }

    /// <summary>
    /// Configures the high-performance wildcard pattern matcher (supports "order.*", "*.created", "*").
    /// </summary>
    /// <param name="builder">The gateway builder being configured.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseWildcardMatcher(this IWebhookPublishingBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSubscriptionMatcher>();
        builder.Services.AddSingleton<IWebhookSubscriptionMatcher, CompositeSubscriptionMatcher>();
        return builder;
    }

    /// <summary>
    /// Configures a custom subscription matching algorithm type.
    /// </summary>
    /// <typeparam name="TMatcher">The type implementing <see cref="IWebhookSubscriptionMatcher"/>.</typeparam>
    /// <param name="builder">The gateway builder being configured.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookPublishingBuilder UseMatcher<TMatcher>(this IWebhookPublishingBuilder builder)
        where TMatcher : class, IWebhookSubscriptionMatcher {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookSubscriptionMatcher>();
        builder.Services.AddSingleton<IWebhookSubscriptionMatcher, TMatcher>();
        return builder;
    }

    /// <summary>
    /// Configures a custom topic pattern matcher.
    /// </summary>
    public static IWebhookPublishingBuilder UseTopicMatcher<TMatcher>(this IWebhookPublishingBuilder builder)
        where TMatcher : class, IWebhookTopicMatcher {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookTopicMatcher>();
        builder.Services.AddSingleton<IWebhookTopicMatcher, TMatcher>();
        return builder;
    }

    /// <summary>
    /// Configures a custom content filter evaluator.
    /// </summary>
    public static IWebhookPublishingBuilder UseContentEvaluator<TEvaluator>(this IWebhookPublishingBuilder builder)
        where TEvaluator : class, IWebhookContentFilterEvaluator {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookContentFilterEvaluator>();
        builder.Services.AddSingleton<IWebhookContentFilterEvaluator, TEvaluator>();
        return builder;
    }

    /// <summary>
    /// Enables persistent 1-to-N batch tracking and self-healing recovery using the in-memory store.
    /// </summary>
    public static IWebhookPublishingBuilder UsePersistentBatching(this IWebhookPublishingBuilder builder) {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookBatchStore>();
        builder.Services.AddSingleton<IWebhookBatchStore, InMemoryWebhookBatchStore>();
        builder.Services.AddHostedService<StaleBatchRecoveryService>();
        return builder;
    }

    /// <summary>
    /// Configures a custom persistent batch store (e.g. EF Core / PostgreSQL).
    /// </summary>
    public static IWebhookPublishingBuilder UseBatchStore<TStore>(this IWebhookPublishingBuilder builder)
        where TStore : class, IWebhookBatchStore {
        Preca.ThrowIfNull(builder);
        builder.Services.RemoveAll<IWebhookBatchStore>();
        builder.Services.AddSingleton<IWebhookBatchStore, TStore>();
        builder.Services.AddHostedService<StaleBatchRecoveryService>();
        return builder;
    }
}