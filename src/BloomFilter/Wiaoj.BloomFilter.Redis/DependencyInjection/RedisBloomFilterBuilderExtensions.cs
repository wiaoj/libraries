using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Engine;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.BloomFilter.Redis.Storage;
using Wiaoj.Preconditions;

#pragma warning disable IDE0130
namespace Wiaoj.BloomFilter;
#pragma warning restore IDE0130

/// <summary>
/// Extension methods for configuring Redis-backed storage, distributed filters, and synchronized filters.
/// </summary>
public static class RedisBloomFilterBuilderExtensions {

    #region Redis Connection Setup

    /// <summary>
    /// Configures the Redis connection using an existing <see cref="IConnectionMultiplexer"/> instance.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="multiplexer">The connection multiplexer instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedis(
        this IBloomFilterBuilder builder,
        IConnectionMultiplexer multiplexer) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(multiplexer);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(multiplexer);
        return builder;
    }

    /// <summary>
    /// Configures the Redis connection using a connection string.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedis(
        this IBloomFilterBuilder builder,
        string connectionString) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(connectionString);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));
        return builder;
    }

    /// <summary>
    /// Configures the Redis connection using <see cref="ConfigurationOptions"/>.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="options">The Redis configuration options.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedis(
        this IBloomFilterBuilder builder,
        ConfigurationOptions options) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(options);

        builder.Services.TryAddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        return builder;
    }

    #endregion

    #region Snapshot Storage (Model 1)

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis using an existing <see cref="IConnectionMultiplexer"/> in DI.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(this IBloomFilterBuilder builder) {
        return builder.UseRedisStorage(_ => { });
    }

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis with custom options using an existing <see cref="IConnectionMultiplexer"/> in DI.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="configure">The storage options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(
        this IBloomFilterBuilder builder,
        Action<RedisBloomFilterStorageOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);
        builder.Services.RemoveAll<IBloomFilterStorage>();
        builder.Services.AddSingleton<IBloomFilterStorage, RedisBloomFilterStorage>();
        return builder;
    }

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis using a connection string.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(
        this IBloomFilterBuilder builder,
        string connectionString) {
        return builder.UseRedisStorage(connectionString, _ => { });
    }

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis using a connection string and custom options.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="connectionString">The Redis connection string.</param>
    /// <param name="configure">The storage options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(
        this IBloomFilterBuilder builder,
        string connectionString,
        Action<RedisBloomFilterStorageOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(connectionString);
        Preca.ThrowIfNull(configure);

        builder.UseRedis(connectionString);
        return builder.UseRedisStorage(configure);
    }

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis using an explicit <see cref="IConnectionMultiplexer"/> instance.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="multiplexer">The connection multiplexer instance.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(
        this IBloomFilterBuilder builder,
        IConnectionMultiplexer multiplexer) {
        return builder.UseRedisStorage(multiplexer, _ => { });
    }

    /// <summary>
    /// Configures the Bloom Filter engine to persist snapshots to Redis using an explicit <see cref="IConnectionMultiplexer"/> instance and custom options.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="multiplexer">The connection multiplexer instance.</param>
    /// <param name="configure">The storage options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder UseRedisStorage(
        this IBloomFilterBuilder builder,
        IConnectionMultiplexer multiplexer,
        Action<RedisBloomFilterStorageOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNull(multiplexer);
        Preca.ThrowIfNull(configure);

        builder.UseRedis(multiplexer);
        return builder.UseRedisStorage(configure);
    }

    #endregion

    #region Distributed Remote Filter (Model 2)

    /// <summary>
    /// Registers a distributed remote Bloom Filter stored in Redis and linked to a marker type tag.
    /// </summary>
    /// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddDistributedFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {
        return builder.AddDistributedFilter<TTag>(name, expectedItems, errorRate, _ => { });
    }

    /// <summary>
    /// Registers a distributed remote Bloom Filter stored in Redis with custom options and linked to a marker type tag.
    /// </summary>
    /// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddDistributedFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        Action<DistributedBloomFilterOptions> configure) where TTag : notnull {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.TryAddSingleton<DistributedRedisBloomFilter<TTag>>(sp => {
            IConnectionMultiplexer redis = sp.GetRequiredService<IConnectionMultiplexer>();
            IOptions<DistributedBloomFilterOptions> options = sp.GetRequiredService<IOptions<DistributedBloomFilterOptions>>();

            IBloomFilterConfigurationFactory configFactory = sp.GetService<IBloomFilterConfigurationFactory>() ?? new BloomFilterConfigurationFactory();
            IOptions<BloomFilterOptions>? bfOptions = sp.GetService<IOptions<BloomFilterOptions>>();
            long hashSeed = bfOptions?.Value.DefaultHashSeed ?? BloomFilterConfiguration.DefaultHashSeed;
            BloomFilterConfiguration config = configFactory.Create(FilterName.Parse(name), expectedItems, errorRate, hashSeed);

            return new DistributedRedisBloomFilter<TTag>(redis, config, options);
        });

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => sp.GetRequiredService<DistributedRedisBloomFilter<TTag>>());
        builder.Services.TryAddSingleton<IAsyncBloomFilter<TTag>>(sp => sp.GetRequiredService<DistributedRedisBloomFilter<TTag>>());

        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, _) => sp.GetRequiredService<DistributedRedisBloomFilter<TTag>>());
        builder.Services.TryAddKeyedSingleton<IAsyncBloomFilter>(name, (sp, _) => sp.GetRequiredService<DistributedRedisBloomFilter<TTag>>());

        return builder;
    }

    /// <summary>
    /// Registers a distributed remote Bloom Filter stored in Redis.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddDistributedFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) {
        return builder.AddDistributedFilter(name, expectedItems, errorRate, _ => { });
    }

    /// <summary>
    /// Registers a distributed remote Bloom Filter stored in Redis with custom options.
    /// </summary>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddDistributedFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        Action<DistributedBloomFilterOptions> configure) {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, _) => {
            IConnectionMultiplexer redis = sp.GetRequiredService<IConnectionMultiplexer>();
            IOptions<DistributedBloomFilterOptions> options = sp.GetRequiredService<IOptions<DistributedBloomFilterOptions>>();

            IBloomFilterConfigurationFactory configFactory = sp.GetService<IBloomFilterConfigurationFactory>() ?? new BloomFilterConfigurationFactory();
            IOptions<BloomFilterOptions>? bfOptions = sp.GetService<IOptions<BloomFilterOptions>>();
            long hashSeed = bfOptions?.Value.DefaultHashSeed ?? BloomFilterConfiguration.DefaultHashSeed;
            BloomFilterConfiguration config = configFactory.Create(FilterName.Parse(name), expectedItems, errorRate, hashSeed);

            return new DistributedRedisBloomFilter(redis, config, options);
        });

        builder.Services.TryAddKeyedSingleton<IAsyncBloomFilter>(name, (sp, _) =>
            (IAsyncBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(name));

        return builder;
    }

    #endregion

    #region Hybrid Synchronized Filter (Model 3)

    /// <summary>
    /// Registers a hybrid synchronized Bloom Filter combining L1 SIMD in-memory performance
    /// with Redis Pub/Sub delta replication and linked to a marker type tag.
    /// </summary>
    /// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddSynchronizedFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {
        return builder.AddSynchronizedFilter<TTag>(name, expectedItems, errorRate, _ => { });
    }

    /// <summary>
    /// Registers a hybrid synchronized Bloom Filter combining L1 SIMD in-memory performance
    /// with Redis Pub/Sub delta replication, custom options, and linked to a marker type tag.
    /// </summary>
    /// <typeparam name="TTag">The marker type identifying the filter domain context.</typeparam>
    /// <param name="builder">The Bloom Filter builder.</param>
    /// <param name="name">The unique filter identifier name.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive probability.</param>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public static IBloomFilterBuilder AddSynchronizedFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        Action<SynchronizedBloomFilterOptions> configure) where TTag : notnull {
        Preca.ThrowIfNull(builder);
        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfNull(configure);

        builder.Services.Configure(configure);

        // Register filter definition in options so BloomFilterFactory can create and hydrate the in-memory engine
        builder.Services.Configure<BloomFilterOptions>(options => {
            options.Filters[name] = new FilterDefinition {
                ExpectedItems = expectedItems,
                ErrorRate = errorRate,
                Type = BloomFilterType.InMemory
            };
        });

        builder.Services.TryAddSingleton<SynchronizedRedisBloomFilter<TTag>>(sp => {
            IConnectionMultiplexer redis = sp.GetRequiredService<IConnectionMultiplexer>();
            IOptions<SynchronizedBloomFilterOptions> options = sp.GetRequiredService<IOptions<SynchronizedBloomFilterOptions>>();
            BloomFilterFactory factory = sp.GetRequiredService<BloomFilterFactory>();

            IPersistentBloomFilter leafFilter = factory.Create(FilterName.Parse(name)).GetAwaiter().GetResult();
            if (leafFilter is not InMemoryBloomFilter inMemory) {
                throw new InvalidOperationException($"Filter '{name}' must be an {nameof(InMemoryBloomFilter)}.");
            }

            ILogger<SynchronizedRedisBloomFilter> logger = sp.GetService<ILogger<SynchronizedRedisBloomFilter>>()
                ?? NullLogger<SynchronizedRedisBloomFilter>.Instance;

            return new SynchronizedRedisBloomFilter<TTag>(redis, inMemory, options, logger);
        });

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => sp.GetRequiredService<SynchronizedRedisBloomFilter<TTag>>());
        builder.Services.TryAddSingleton<IAsyncBloomFilter<TTag>>(sp => sp.GetRequiredService<SynchronizedRedisBloomFilter<TTag>>());

        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, _) => sp.GetRequiredService<SynchronizedRedisBloomFilter<TTag>>());
        builder.Services.TryAddKeyedSingleton<IAsyncBloomFilter>(name, (sp, _) => sp.GetRequiredService<SynchronizedRedisBloomFilter<TTag>>());
        builder.Services.TryAddKeyedSingleton<IPersistentBloomFilter>(name, (sp, _) => sp.GetRequiredService<SynchronizedRedisBloomFilter<TTag>>());

        return builder;
    }

    #endregion
}
