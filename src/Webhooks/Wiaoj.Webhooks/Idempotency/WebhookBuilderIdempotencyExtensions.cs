using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.Webhooks.Idempotency;

#pragma warning disable IDE0130
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring outbound idempotency and deduplication on <see cref="IWebhookBuilder"/>.
/// </summary>
public static partial class WebhookBuilderIdempotencyExtensions {
    /// <summary>
    /// Configures deterministic idempotency enforcement using default in-memory store and 24-hour sliding window.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(this IWebhookBuilder builder) {
        return UseIdempotency(builder, new IdempotencyOptions());
    }

    /// <summary>
    /// Configures deterministic idempotency enforcement with the specified sliding expiration window.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="window">The time window during which duplicate events are suppressed.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="window"/> is non-positive.</exception>
    public static IWebhookBuilder UseIdempotency(this IWebhookBuilder builder, TimeSpan window) {
        return UseIdempotency(builder, new IdempotencyOptions { Window = window });
    }

    /// <summary>
    /// Configures deterministic idempotency enforcement with custom options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The idempotency configuration options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(this IWebhookBuilder builder, IdempotencyOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IIdempotencyKeyGenerator, DefaultIdempotencyKeyGenerator>();
        builder.Services.TryAddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        builder.AddMiddleware<IdempotencyMiddleware>();

        return builder;
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator generic types.
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IIdempotencyStore"/>.</typeparam>
    /// <typeparam name="TKeyGenerator">The type implementing <see cref="IIdempotencyKeyGenerator"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">An optional delegate to configure <see cref="IdempotencyOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency<TStore, TKeyGenerator>(
        this IWebhookBuilder builder,
        Action<IdempotencyOptions>? configure = null)
        where TStore : class, IIdempotencyStore
        where TKeyGenerator : class, IIdempotencyKeyGenerator {

        Preca.ThrowIfNull(builder);

        IdempotencyOptions options = new();
        configure?.Invoke(options);
        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.RemoveAll<IIdempotencyStore>();
        builder.Services.RemoveAll<IIdempotencyKeyGenerator>();
        builder.Services.AddSingleton<IIdempotencyStore, TStore>();
        builder.Services.AddSingleton<IIdempotencyKeyGenerator, TKeyGenerator>();
        builder.AddMiddleware<IdempotencyMiddleware>();

        return builder;
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator instances.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="store">The idempotency store instance.</param>
    /// <param name="keyGenerator">The idempotency key generator instance (optional, defaults to standard SIMD generator).</param>
    /// <param name="configureOptions">An optional delegate to configure options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(
        this IWebhookBuilder builder,
        IIdempotencyStore store,
        IIdempotencyKeyGenerator? keyGenerator = null,
        Action<IdempotencyOptions>? configureOptions = null) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(store);

        IdempotencyOptions options = new();
        configureOptions?.Invoke(options);
        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.RemoveAll<IIdempotencyStore>();
        builder.Services.AddSingleton(store);

        if(keyGenerator is not null) {
            builder.Services.RemoveAll<IIdempotencyKeyGenerator>();
            builder.Services.AddSingleton(keyGenerator);
        }
        else {
            builder.Services.TryAddSingleton<IIdempotencyKeyGenerator, DefaultIdempotencyKeyGenerator>();
        }

        builder.AddMiddleware<IdempotencyMiddleware>();
        return builder;
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator factory delegates.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="storeFactory">The factory delegate used to resolve the idempotency store.</param>
    /// <param name="keyGeneratorFactory">The factory delegate used to resolve the key generator.</param>
    /// <param name="configureOptions">An optional delegate to configure options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="storeFactory"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(
        this IWebhookBuilder builder,
        Func<IServiceProvider, IIdempotencyStore> storeFactory,
        Func<IServiceProvider, IIdempotencyKeyGenerator>? keyGeneratorFactory = null,
        Action<IdempotencyOptions>? configureOptions = null) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(storeFactory);

        IdempotencyOptions options = new();
        configureOptions?.Invoke(options);
        options.Validate();

        builder.Services.AddSingleton(options);
        builder.Services.RemoveAll<IIdempotencyStore>();
        builder.Services.AddSingleton(storeFactory);

        if(keyGeneratorFactory is not null) {
            builder.Services.RemoveAll<IIdempotencyKeyGenerator>();
            builder.Services.AddSingleton(keyGeneratorFactory);
        }
        else {
            builder.Services.TryAddSingleton<IIdempotencyKeyGenerator, DefaultIdempotencyKeyGenerator>();
        }

        builder.AddMiddleware<IdempotencyMiddleware>();
        return builder;
    }
}