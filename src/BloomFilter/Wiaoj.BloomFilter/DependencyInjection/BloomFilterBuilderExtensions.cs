using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Extension methods for configuring and registering Bloom Filters via explicit method overloads.
/// </summary>
public static class BloomFilterBuilderExtensions {

    #region Standard In-Memory Filter Overloads

    /// <summary>
    /// Registers a standard In-Memory Bloom Filter linked to a marker type tag.
    /// </summary>
    public static IBloomFilterBuilder AddInMemoryFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);

        FilterDefinition definition = new() {
            ExpectedItems = expectedItems,
            ErrorRate = errorRate,
            Type = BloomFilterType.InMemory
        };

        return builder.RegisterFilterDefinition<TTag>(name, definition);
    }

    /// <summary>
    /// Registers a standard In-Memory Bloom Filter.
    /// </summary>
    public static IBloomFilterBuilder AddInMemoryFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);

        FilterDefinition definition = new() {
            ExpectedItems = expectedItems,
            ErrorRate = errorRate,
            Type = BloomFilterType.InMemory
        };

        return builder.RegisterFilterDefinition(name, definition);
    }

    /// <summary>
    /// Alias for <see cref="AddInMemoryFilter{TTag}"/>.
    /// </summary>
    public static IBloomFilterBuilder AddFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {
        return builder.AddInMemoryFilter<TTag>(name, expectedItems, errorRate);
    }

    /// <summary>
    /// Alias for <see cref="AddInMemoryFilter"/>.
    /// </summary>
    public static IBloomFilterBuilder AddFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) {
        return builder.AddInMemoryFilter(name, expectedItems, errorRate);
    }

    #endregion

    #region Rotating Filter Overloads

    /// <summary>
    /// Registers a sliding-window Rotating Bloom Filter linked to a marker type tag.
    /// </summary>
    public static IBloomFilterBuilder AddRotatingFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        TimeSpan windowSize,
        int shardCount) where TTag : notnull {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfLessThan(shardCount, 1);

        FilterDefinition definition = new() {
            ExpectedItems = expectedItems,
            ErrorRate = errorRate,
            Type = BloomFilterType.Rotating,
            WindowSize = windowSize,
            ShardCount = shardCount
        };

        return builder.RegisterFilterDefinition<TTag>(name, definition);
    }

    /// <summary>
    /// Registers a sliding-window Rotating Bloom Filter.
    /// </summary>
    public static IBloomFilterBuilder AddRotatingFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        TimeSpan windowSize,
        int shardCount) {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(expectedItems);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfLessThan(shardCount, 1);

        FilterDefinition definition = new() {
            ExpectedItems = expectedItems,
            ErrorRate = errorRate,
            Type = BloomFilterType.Rotating,
            WindowSize = windowSize,
            ShardCount = shardCount
        };

        return builder.RegisterFilterDefinition(name, definition);
    }

    #endregion

    #region Scalable Filter Overloads

    /// <summary>
    /// Registers a Scalable Bloom Filter linked to a marker type tag using default double growth and 50% saturation.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate) where TTag : notnull {
        return builder.AddScalableFilter<TTag>(name, initialCapacity, errorRate, GrowthRate.Double, 0.50);
    }

    /// <summary>
    /// Registers a Scalable Bloom Filter linked to a marker type tag with custom growth rate.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate,
        GrowthRate growthRate) where TTag : notnull {
        return builder.AddScalableFilter<TTag>(name, initialCapacity, errorRate, growthRate, 0.50);
    }

    /// <summary>
    /// Registers a Scalable Bloom Filter linked to a marker type tag with custom growth rate and saturation threshold.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate,
        GrowthRate growthRate,
        double saturationThreshold) where TTag : notnull {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(initialCapacity);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfNotBetweenExclusive(saturationThreshold, 0.0, 1.0);

        FilterDefinition definition = new() {
            ExpectedItems = initialCapacity,
            ErrorRate = errorRate,
            Type = BloomFilterType.Scalable,
            GrowthRate = growthRate.Value,
            SaturationThreshold = saturationThreshold
        };

        return builder.RegisterFilterDefinition<TTag>(name, definition);
    }

    /// <summary>
    /// Registers a Scalable Bloom Filter using default double growth and 50% saturation.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate) {
        return builder.AddScalableFilter(name, initialCapacity, errorRate, GrowthRate.Double, 0.50);
    }

    /// <summary>
    /// Registers a Scalable Bloom Filter with custom growth rate.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate,
        GrowthRate growthRate) {
        return builder.AddScalableFilter(name, initialCapacity, errorRate, growthRate, 0.50);
    }

    /// <summary>
    /// Registers a Scalable Bloom Filter with custom growth rate and saturation threshold.
    /// </summary>
    public static IBloomFilterBuilder AddScalableFilter(
        this IBloomFilterBuilder builder,
        string name,
        long initialCapacity,
        double errorRate,
        GrowthRate growthRate,
        double saturationThreshold) {

        Preca.ThrowIfNullOrWhiteSpace(name);
        Preca.ThrowIfNegativeOrZero(initialCapacity);
        Preca.ThrowIfNotBetweenExclusive(errorRate, BloomFilterConfiguration.MinimumErrorRate, BloomFilterConfiguration.MaximumErrorRate);
        Preca.ThrowIfNotBetweenExclusive(saturationThreshold, 0.0, 1.0);

        FilterDefinition definition = new() {
            ExpectedItems = initialCapacity,
            ErrorRate = errorRate,
            Type = BloomFilterType.Scalable,
            GrowthRate = growthRate.Value,
            SaturationThreshold = saturationThreshold
        };

        return builder.RegisterFilterDefinition(name, definition);
    }

    #endregion

    #region AppSettings Mapping & Storage

    /// <summary>
    /// Maps a named filter configured in appsettings.json to a strongly-typed marker tag.
    /// </summary>
    public static IBloomFilterBuilder MapFilter<TTag>(this IBloomFilterBuilder builder, string filterName)
        where TTag : notnull {

        builder.RegisterFilter(filterName);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            IBloomFilter innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(filterName);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    /// <summary>
    /// Registers a custom persistent storage provider.
    /// </summary>
    public static IBloomFilterBuilder AddStorage<TStorage>(this IBloomFilterBuilder builder)
        where TStorage : class, IBloomFilterStorage {
        builder.Services.Replace(ServiceDescriptor.Singleton<IBloomFilterStorage, TStorage>());
        return builder;
    }

    #endregion

    #region Internal Registration Helpers

    private static IBloomFilterBuilder RegisterFilterDefinition<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        FilterDefinition definition) where TTag : notnull {

        builder.RegisterFilterDefinition(name, definition);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            IBloomFilter innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(name);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    private static IBloomFilterBuilder RegisterFilterDefinition(
        this IBloomFilterBuilder builder,
        string name,
        FilterDefinition definition) {

        builder.Services.Configure<BloomFilterOptions>(options => {
            options.Filters[name] = definition;
        });

        return builder.RegisterFilter(name);
    }

    private static IBloomFilterBuilder RegisterFilter(this IBloomFilterBuilder builder, string name) {
        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, key) => {
            BloomFilterFactory factory = sp.GetRequiredService<BloomFilterFactory>();
            IBloomFilterRegistry registry = sp.GetRequiredService<IBloomFilterRegistry>();
            ILoggerFactory loggerFactory = sp.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
            return new LazyBloomFilterProxy(key?.ToString() ?? string.Empty, factory, registry, loggerFactory);
        });

        builder.Services.TryAddKeyedSingleton<IPersistentBloomFilter>(name, (sp, key) =>
            (IPersistentBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(key));

        return builder;
    }

    #endregion
}