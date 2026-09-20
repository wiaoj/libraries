using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Idempotency.Redis;
#pragma warning restore IDE0130

/// <summary>
/// Registration for the Redis-backed idempotency store.
/// </summary>
public static class RedisIdempotencyServiceCollectionExtensions {
    /// <summary>
    /// Registers <see cref="RedisIdempotencyStore"/> over the <see cref="IConnectionMultiplexer"/> already registered in
    /// the container, so it shares one connection with anything else using Redis.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional key prefix and database configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRedisIdempotencyStore(
        this IServiceCollection services,
        Action<RedisIdempotencyOptions>? configure = null) {
        Preca.ThrowIfNull(services);

        RedisIdempotencyOptions options = Configured(configure);

        services.RemoveAll<IIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore>(sp =>
            new RedisIdempotencyStore(sp.GetRequiredService<IConnectionMultiplexer>(), options));

        return services;
    }

    /// <summary>
    /// Registers the store, connecting with the given connection string when no multiplexer is registered yet.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="configure">Optional key prefix and database configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRedisIdempotencyStore(
        this IServiceCollection services,
        string connectionString,
        Action<RedisIdempotencyOptions>? configure = null) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNullOrWhiteSpace(connectionString);

        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));

        return services.AddRedisIdempotencyStore(configure);
    }

    /// <summary>
    /// Registers the store, connecting with the given <see cref="ConfigurationOptions"/> when no multiplexer is
    /// registered yet.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configurationOptions">The StackExchange.Redis configuration.</param>
    /// <param name="configure">Optional key prefix and database configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddRedisIdempotencyStore(
        this IServiceCollection services,
        ConfigurationOptions configurationOptions,
        Action<RedisIdempotencyOptions>? configure = null) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(configurationOptions);

        services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(configurationOptions));

        return services.AddRedisIdempotencyStore(configure);
    }

    /// <summary>
    /// Registers the store over a keyed <see cref="IConnectionMultiplexer"/>, for an application that keeps several
    /// Redis connections.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serviceKey">The key the multiplexer is registered under.</param>
    /// <param name="configure">Optional key prefix and database configuration.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddKeyedRedisIdempotencyStore(
        this IServiceCollection services,
        object serviceKey,
        Action<RedisIdempotencyOptions>? configure = null) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(serviceKey);

        RedisIdempotencyOptions options = Configured(configure);

        services.RemoveAll<IIdempotencyStore>();
        services.AddSingleton<IIdempotencyStore>(sp =>
            new RedisIdempotencyStore(sp.GetRequiredKeyedService<IConnectionMultiplexer>(serviceKey), options));

        return services;
    }

    private static RedisIdempotencyOptions Configured(Action<RedisIdempotencyOptions>? configure) {
        RedisIdempotencyOptions options = new();
        configure?.Invoke(options);
        return options;
    }
}
