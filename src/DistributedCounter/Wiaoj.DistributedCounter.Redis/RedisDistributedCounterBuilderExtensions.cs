using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Wiaoj.DistributedCounter.DependencyInjection;
using Wiaoj.DistributedCounter.Redis.Internal;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.DistributedCounter;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Extension methods for configuring Redis-backed storage on <see cref="IDistributedCounterBuilder"/>.
/// </summary>
public static class RedisDistributedCounterBuilderExtensions {
    /// <summary>
    /// Configures the distributed counter to use Redis via connection string.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="connectionString">The Redis connection string (e.g. "localhost:6379,abortConnect=false").</param>
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
    /// Configures the distributed counter to use Redis via <see cref="ConfigurationOptions"/>.
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
    /// Configures the distributed counter to use Redis with an existing <see cref="IConnectionMultiplexer"/> singleton instance.
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
    /// Configures the distributed counter to use Redis with a custom factory delegate.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="multiplexerFactory">Factory resolving the connection multiplexer from the container.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedis(
        this IDistributedCounterBuilder builder,
        Func<IServiceProvider, IConnectionMultiplexer> multiplexerFactory) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(multiplexerFactory);

        builder.Services.RemoveAll<IConnectionMultiplexer>();
        builder.Services.AddSingleton(multiplexerFactory);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage, RedisCounterStorage>();

        return builder;
    }

    /// <summary>
    /// Configures the distributed counter to use Redis, assuming <see cref="IConnectionMultiplexer"/> is already registered in DI.
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
    /// Configures the distributed counter to use Redis resolving a Keyed <see cref="IConnectionMultiplexer"/> from DI.
    /// </summary>
    /// <param name="builder">The counter builder.</param>
    /// <param name="serviceKey">The keyed service identifier for the multiplexer.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IDistributedCounterBuilder UseRedisKeyed(
        this IDistributedCounterBuilder builder,
        object? serviceKey) {
        Preca.ThrowIfNull(builder);

        builder.Services.RemoveAll<ICounterStorage>();
        builder.Services.AddSingleton<ICounterStorage>(sp => {
            IConnectionMultiplexer multiplexer = sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey);
            return new RedisCounterStorage(multiplexer);
        });

        return builder;
    }
}