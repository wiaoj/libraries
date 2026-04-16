using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter; 
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
public static class BloomFilterBuilderExtensions {

    /// <summary>
    /// Filtreyi özelleştirilebilir bir konfigürasyon ile tanımlar.
    /// </summary>
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
    /// Sadece ayarları konfigürasyonda (appsettings) olan bir filtreyi DI Container'a tanıtır.
    /// </summary>
    public static IBloomFilterBuilder RegisterFilter(this IBloomFilterBuilder builder, string name) {
        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, key) => {
            var factory = sp.GetRequiredService<BloomFilterFactory>();
            var registry = sp.GetRequiredService<IBloomFilterRegistry>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LazyBloomFilterProxy((string)key, factory, registry, loggerFactory);
        });

        builder.Services.TryAddKeyedSingleton<IPersistentBloomFilter>(name, (sp, key) =>
            (IPersistentBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(key));

        return builder;
    }

    /// <summary>
    /// Kod üzerinden "Tipli (Typed)" bir filtre kaydeder.
    /// </summary>
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
    /// Konfigürasyonda zaten tanımlı olan bir ismi, Tipli Interface'e bağlar.
    /// </summary>
    public static IBloomFilterBuilder MapFilter<TTag>(this IBloomFilterBuilder builder, string filterName)
        where TTag : notnull {

        builder.RegisterFilter(filterName);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            var innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(filterName);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    // --- Diğer Helper Metotlar ---

    public static IBloomFilterBuilder Configure(this IBloomFilterBuilder builder, Action<BloomFilterOptions> configure) {
        builder.Services.Configure(configure);
        return builder;
    }

    public static IBloomFilterBuilder AddStorage<TStorage>(this IBloomFilterBuilder builder)
        where TStorage : class, IBloomFilterStorage {
        builder.Services.Replace(ServiceDescriptor.Singleton<IBloomFilterStorage, TStorage>());
        return builder;
    }

    public static IBloomFilterBuilder AddAutoSave(this IBloomFilterBuilder builder) {
        builder.Services.AddHostedService<BloomFilterAutoSaveService>();
        return builder;
    }
}