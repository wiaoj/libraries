using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
/// <summary>
/// Extension methods for <see cref="BloomFilterBuilder"/> to configure filters, storage, and services.
/// </summary>
public static class BloomFilterBuilderExtensions {

    /// <summary>
    /// Adds a named Bloom Filter configuration via code.
    /// </summary>
    public static BloomFilterBuilder AddFilter(
        this BloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) {

        // Mantık: Doğrudan Options nesnesini configure ediyoruz.
        builder.Services.Configure<BloomFilterOptions>(options => {
            options.Filters[name] = new FilterDefinition {
                ExpectedItems = expectedItems,
                ErrorRate = errorRate
            };
        });

        return builder;
    }

    /// <summary>
    /// Maps a strongly-typed <see cref="IBloomFilter{TTag}"/> to a named filter configuration.
    /// Also registers the filter configuration if not already present via JSON.
    /// </summary>
    public static BloomFilterBuilder AddFilter<TTag>(
        this BloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {

        // 1. Config'i ekle
        builder.AddFilter(name, expectedItems, errorRate);

        // 2. Typed Wrapper'ı DI'a ekle
        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            var provider = sp.GetRequiredService<IBloomFilterProvider>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new TypedBloomFilterWrapper<TTag>(provider, name, loggerFactory);
        });

        return builder;
    }

    /// <summary>
    /// Maps an existing named filter (from appsettings) to a typed interface.
    /// </summary>
    public static BloomFilterBuilder MapFilter<TTag>(this BloomFilterBuilder builder, string filterName)
        where TTag : notnull {

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            var provider = sp.GetRequiredService<IBloomFilterProvider>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new TypedBloomFilterWrapper<TTag>(provider, filterName, loggerFactory);
        });
        return builder;
    }

    /// <summary>
    /// Configures additional options using a delegate.
    /// </summary>
    public static BloomFilterBuilder Configure(this BloomFilterBuilder builder, Action<BloomFilterOptions> configure) {
        builder.Services.Configure(configure);
        return builder;
    }

    /// <summary>
    /// Overrides the default file system storage with a custom implementation.
    /// </summary>
    public static BloomFilterBuilder AddStorage<TStorage>(this BloomFilterBuilder builder)
        where TStorage : class, IBloomFilterStorage {
        builder.Services.Replace(ServiceDescriptor.Singleton<IBloomFilterStorage, TStorage>());
        return builder;
    }

    /// <summary>
    /// Explicitly enables the auto-save background service (enabled by default).
    /// </summary>
    public static BloomFilterBuilder AddAutoSave(this BloomFilterBuilder builder) {
        builder.Services.AddHostedService<BloomFilterAutoSaveService>();
        return builder;
    }
}