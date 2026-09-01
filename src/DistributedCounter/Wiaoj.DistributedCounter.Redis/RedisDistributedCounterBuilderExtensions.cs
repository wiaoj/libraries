using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring Redis-backed storage on <see cref="IDistributedCounterBuilder"/> and <see cref="CounterConfiguration"/>.
/// </summary>
public static class RedisDistributedCounterBuilderExtensions {
    // ── 1. Global Builder Extensions (Engine Storage Defaults) ───────────────────

    /// <summary>
    /// Configures the distributed counter engine to use an existing <see cref="IConnectionMultiplexer"/> already registered in the service collection.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(this IDistributedCounterBuilder builder) {
        Preca.ThrowIfNull(builder);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis via connection string as the global default storage.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        string connectionString) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(connectionString);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis via a dynamic connection string factory.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="connectionStringFactory">The factory function to resolve the connection string from the service provider.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        Func<IServiceProvider, string> connectionStringFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(connectionStringFactory);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(sp => {
            string connectionString = connectionStringFactory(sp);
            Preca.ThrowIfNullOrWhiteSpace(connectionString);
            return ConnectionMultiplexer.Connect(connectionString);
        });

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis via <see cref="ConfigurationOptions"/>.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="configurationOptions">The StackExchange.Redis configuration options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        ConfigurationOptions configurationOptions) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configurationOptions);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions));
        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis via a dynamic <see cref="ConfigurationOptions"/> factory.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="configurationOptionsFactory">The factory function to resolve configuration options from the service provider.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        Func<IServiceProvider, ConfigurationOptions> configurationOptionsFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configurationOptionsFactory);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(sp => {
            ConfigurationOptions configurationOptions = configurationOptionsFactory(sp);
            Preca.ThrowIfNull(configurationOptions);
            return ConnectionMultiplexer.Connect(configurationOptions);
        });

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis with an existing <see cref="IConnectionMultiplexer"/> singleton instance.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="connectionMultiplexer">The active multiplexer instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        IConnectionMultiplexer connectionMultiplexer) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(connectionMultiplexer);

        builder.Services.RemoveAll<IConnectionMultiplexer>();
        builder.Services.AddSingleton(connectionMultiplexer);
        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter engine to use Redis via a custom <see cref="IConnectionMultiplexer"/> factory.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="multiplexerFactory">The factory function to resolve the active multiplexer instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        Func<IServiceProvider, IConnectionMultiplexer> multiplexerFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(multiplexerFactory);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(sp => {
            IConnectionMultiplexer multiplexer = multiplexerFactory(sp);
            Preca.ThrowIfNull(multiplexer);
            return new RedisCounterStorage(multiplexer);
        });

        return builder;
    }

    /// <summary>
    /// Configures the global counter engine to use Redis via a keyed <see cref="IConnectionMultiplexer"/> service.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="serviceKey">The keyed service identifier for the multiplexer.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedisKeyed(
        this IDistributedCounterBuilder builder,
        object serviceKey) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(serviceKey);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(sp => {
            IConnectionMultiplexer multiplexer = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
            return new RedisCounterStorage(multiplexer);
        });

        return builder;
    }

    // ── 2. Per-Counter / Per-Tag Configuration Extensions ─────────────────────────

    /// <summary>
    /// Configures this specific counter or tag to use Redis resolved from the service provider.
    /// </summary>
    /// <param name="config">The counter configuration.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public static CounterConfiguration UseRedis(this CounterConfiguration config) {
        Preca.ThrowIfNull(config);
        return config.UseStorage(static sp => {
            IConnectionMultiplexer multiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
            return new RedisCounterStorage(multiplexer);
        });
    }

    /// <summary>
    /// Configures this specific counter or tag to use Redis with an explicit <see cref="IConnectionMultiplexer"/> instance.
    /// </summary>
    /// <param name="config">The counter configuration.</param>
    /// <param name="connectionMultiplexer">The active multiplexer instance.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public static CounterConfiguration UseRedis(
        this CounterConfiguration config,
        IConnectionMultiplexer connectionMultiplexer) {
        Preca.ThrowIfNull(config);
        Preca.ThrowIfNull(connectionMultiplexer);

        return config.UseStorage(_ => new RedisCounterStorage(connectionMultiplexer));
    }

    /// <summary>
    /// Configures this specific counter or tag to use Redis via a custom <see cref="IConnectionMultiplexer"/> factory.
    /// </summary>
    /// <param name="config">The counter configuration.</param>
    /// <param name="multiplexerFactory">The factory function to resolve the active multiplexer instance.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public static CounterConfiguration UseRedis(
        this CounterConfiguration config,
        Func<IServiceProvider, IConnectionMultiplexer> multiplexerFactory) {
        Preca.ThrowIfNull(config);
        Preca.ThrowIfNull(multiplexerFactory);

        return config.UseStorage(sp => {
            IConnectionMultiplexer multiplexer = multiplexerFactory(sp);
            Preca.ThrowIfNull(multiplexer);
            return new RedisCounterStorage(multiplexer);
        });
    }

    /// <summary>
    /// Configures this specific counter or tag to use Redis via a keyed <see cref="IConnectionMultiplexer"/> service.
    /// </summary>
    /// <param name="config">The counter configuration.</param>
    /// <param name="serviceKey">The keyed service identifier for the multiplexer.</param>
    /// <returns>The configuration instance for fluent chaining.</returns>
    public static CounterConfiguration UseRedisKeyed(
        this CounterConfiguration config,
        object serviceKey) {
        Preca.ThrowIfNull(config);
        Preca.ThrowIfNull(serviceKey);

        return config.UseStorage(sp => {
            IConnectionMultiplexer multiplexer = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
            return new RedisCounterStorage(multiplexer);
        });
    }
}