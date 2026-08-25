using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring Redis-backed storage on <see cref="IDistributedCounterBuilder"/> and <see cref="CounterConfiguration"/>.
/// </summary>
public static class RedisDistributedCounterBuilderExtensions {
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