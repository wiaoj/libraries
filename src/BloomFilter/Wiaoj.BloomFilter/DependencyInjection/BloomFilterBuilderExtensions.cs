using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter; 
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// Provides extension methods for <see cref="IBloomFilterBuilder"/> to configure and register Bloom Filters.
/// </summary>
public static class BloomFilterBuilderExtensions {

    /// <summary>
    /// Defines a Bloom Filter with a customizable configuration.
    /// </summary>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="name">The unique name of the filter.</param>
    /// <param name="expectedItems">The expected number of items to be stored.</param>
    /// <param name="errorRate">The desired false positive probability (between 0 and 1).</param>
    /// <param name="configure">An optional action to further configure the filter definition.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder AddFilter(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        Action<FilterDefinition>? configure = null) {

        FilterDefinition def = new() {
            ExpectedItems = expectedItems,
            ErrorRate = errorRate
        };
        configure?.Invoke(def);

        builder.Services.Configure<BloomFilterOptions>(options => {
            options.Filters[name] = def;
        });

        return builder.RegisterFilter(name);
    }

    /// <summary>
    /// Registers a filter defined in configuration (e.g., appsettings.json) into the Dependency Injection container.
    /// </summary>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="name">The unique name of the filter as defined in configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder RegisterFilter(this IBloomFilterBuilder builder, string name) {
        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, key) => {
            var factory = sp.GetRequiredService<BloomFilterFactory>();
            var registry = sp.GetRequiredService<IBloomFilterRegistry>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LazyBloomFilterProxy(key?.ToString() ?? string.Empty, factory, registry, loggerFactory);
        });

        builder.Services.TryAddKeyedSingleton<IPersistentBloomFilter>(name, (sp, key) =>
            (IPersistentBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(key));

        return builder;
    }

    /// <summary>
    /// Registers a strongly-typed Bloom Filter interface linked to a specific filter name.
    /// </summary>
    /// <typeparam name="TTag">The marker type for the filter.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="name">The unique name of the filter.</param>
    /// <param name="expectedItems">The expected number of items.</param>
    /// <param name="errorRate">The target false positive rate.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder AddFilter<TTag>(
        this IBloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        Action<FilterDefinition>? configure = null) where TTag : notnull {

        builder.AddFilter(name, expectedItems, errorRate, configure);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            var innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(name);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    /// <summary>
    /// Maps an existing filter name from configuration to a strongly-typed Bloom Filter interface.
    /// </summary>
    /// <typeparam name="TTag">The marker type for the filter.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="filterName">The name of the filter defined in configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder MapFilter<TTag>(this IBloomFilterBuilder builder, string filterName)
        where TTag : notnull {

        builder.RegisterFilter(filterName);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            var innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(filterName);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    /// <summary>
    /// Configures global <see cref="BloomFilterOptions"/>.
    /// </summary>
    /// <param name="builder">The builder to extend.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder Configure(this IBloomFilterBuilder builder, Action<BloomFilterOptions> configure) {
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Adds a custom storage implementation for Bloom Filters.
    /// </summary>
    /// <typeparam name="TStorage">The type of the storage implementation.</typeparam>
    /// <param name="builder">The builder to extend.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder AddStorage<TStorage>(this IBloomFilterBuilder builder)
        where TStorage : class, IBloomFilterStorage {
        builder.Services.Replace(ServiceDescriptor.Singleton<IBloomFilterStorage, TStorage>());
        return builder;
    }

    /// <summary>
    /// Enables automatic background saving of dirty filters.
    /// </summary>
    /// <param name="builder">The builder to extend.</param>
    /// <returns>The builder for chaining.</returns>
    public static IBloomFilterBuilder AddAutoSave(this IBloomFilterBuilder builder) {
        builder.Services.AddHostedService<BloomFilterAutoSaveService>();
        return builder;
    }
}