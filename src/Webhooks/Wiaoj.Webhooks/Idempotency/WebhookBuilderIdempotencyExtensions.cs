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
    /// Configures deterministic idempotency enforcement using a configuration delegate.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate used to configure <see cref="IdempotencyOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(this IWebhookBuilder builder, Action<IdempotencyOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        IdempotencyOptions options = new();
        configure(options);
        return UseIdempotency(builder, options);
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
    /// Configures deterministic idempotency using custom store and key generator generic types with default options.
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IIdempotencyStore"/>.</typeparam>
    /// <typeparam name="TKeyGenerator">The type implementing <see cref="IIdempotencyKeyGenerator"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency<TStore, TKeyGenerator>(this IWebhookBuilder builder)
        where TStore : class, IIdempotencyStore
        where TKeyGenerator : class, IIdempotencyKeyGenerator {
        return UseIdempotency<TStore, TKeyGenerator>(builder, new IdempotencyOptions());
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator generic types with options delegate.
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IIdempotencyStore"/>.</typeparam>
    /// <typeparam name="TKeyGenerator">The type implementing <see cref="IIdempotencyKeyGenerator"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="configure">The delegate to configure <see cref="IdempotencyOptions"/>.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency<TStore, TKeyGenerator>(
        this IWebhookBuilder builder,
        Action<IdempotencyOptions> configure)
        where TStore : class, IIdempotencyStore
        where TKeyGenerator : class, IIdempotencyKeyGenerator {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        IdempotencyOptions options = new();
        configure(options);
        return UseIdempotency<TStore, TKeyGenerator>(builder, options);
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator generic types with explicit options.
    /// </summary>
    /// <typeparam name="TStore">The type implementing <see cref="IIdempotencyStore"/>.</typeparam>
    /// <typeparam name="TKeyGenerator">The type implementing <see cref="IIdempotencyKeyGenerator"/>.</typeparam>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="options">The idempotency configuration options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency<TStore, TKeyGenerator>(
        this IWebhookBuilder builder,
        IdempotencyOptions options)
        where TStore : class, IIdempotencyStore
        where TKeyGenerator : class, IIdempotencyKeyGenerator {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);
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
    /// Configures deterministic idempotency using custom store instance and default key generator.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="store">The idempotency store instance.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="store"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(
        this IWebhookBuilder builder,
        IIdempotencyStore store) {
        return UseIdempotency(builder, store, new IdempotencyOptions());
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store instance and explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="store">The idempotency store instance.</param>
    /// <param name="options">The idempotency options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="store"/>, or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(
        this IWebhookBuilder builder,
        IIdempotencyStore store,
        IdempotencyOptions options) {
        return UseIdempotency(builder, store, (IIdempotencyKeyGenerator?)null, options);
    }

    /// <summary>
    /// Configures deterministic idempotency using custom store and key generator instances with explicit options.
    /// </summary>
    /// <param name="builder">The webhook builder being configured.</param>
    /// <param name="store">The idempotency store instance.</param>
    /// <param name="keyGenerator">The optional idempotency key generator instance.</param>
    /// <param name="options">The idempotency options.</param>
    /// <returns>The <see cref="IWebhookBuilder"/> instance for fluent method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/>, <paramref name="store"/>, or <paramref name="options"/> is <see langword="null"/>.</exception>
    public static IWebhookBuilder UseIdempotency(
        this IWebhookBuilder builder,
        IIdempotencyStore store,
        IIdempotencyKeyGenerator? keyGenerator,
        IdempotencyOptions options) {

        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(options);
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
}
