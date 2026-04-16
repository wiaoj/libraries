using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130
public static class BloomFilterBuilderExtensions {

    /// <summary>
    /// Hem ayarlarını yapılandırır hem de Keyed Service olarak kaydeder.
    /// </summary>
    public static BloomFilterBuilder AddFilter(
        this BloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate,
        bool isScalable = false,
        double growthRate = 2.0D) {

        builder.Services.Configure<BloomFilterOptions>(options => {
            options.Filters[name] = new FilterDefinition {
                ExpectedItems = expectedItems,
                ErrorRate = errorRate,
                IsScalable = isScalable,
                GrowthRate = growthRate
            };
        });

        return builder.RegisterFilter(name);
    }

    /// <summary>
    /// Sadece ayarları appsettings'de olan bir filtreyi DI Container'a tanıtır.
    /// </summary>
    public static BloomFilterBuilder RegisterFilter(this BloomFilterBuilder builder, string name) {

        builder.Services.TryAddKeyedSingleton<IBloomFilter>(name, (sp, key) => {
            BloomFilterFactory factory = sp.GetRequiredService<BloomFilterFactory>();
            IBloomFilterRegistry registry = sp.GetRequiredService<IBloomFilterRegistry>();
            ILoggerFactory loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            return new LazyBloomFilterProxy((string)key, factory, registry, loggerFactory);
        });

        builder.Services.TryAddKeyedSingleton<IPersistentBloomFilter>(name, (sp, key) =>
            (IPersistentBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(key));

        return builder;
    }

    public static BloomFilterBuilder AddFilter<TTag>(
        this BloomFilterBuilder builder,
        string name,
        long expectedItems,
        double errorRate) where TTag : notnull {

        builder.AddFilter(name, expectedItems, errorRate);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            IBloomFilter innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(name);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    /// <summary>
    /// appsettings'de var olan bir ayarı Typed (Etiketli) Interface'e bağlar.
    /// </summary>
    public static BloomFilterBuilder MapFilter<TTag>(this BloomFilterBuilder builder, string filterName)
        where TTag : notnull {

        // Önce Keyed olarak tanıdığından emin olalım
        builder.RegisterFilter(filterName);

        builder.Services.TryAddSingleton<IBloomFilter<TTag>>(sp => {
            IBloomFilter innerFilter = sp.GetRequiredKeyedService<IBloomFilter>(filterName);
            return new TypedBloomFilterWrapper<TTag>(innerFilter);
        });

        return builder;
    }

    public static BloomFilterBuilder Configure(this BloomFilterBuilder builder, Action<BloomFilterOptions> configure) {
        builder.Services.Configure(configure);
        return builder;
    }

    public static BloomFilterBuilder AddStorage<TStorage>(this BloomFilterBuilder builder)
        where TStorage : class, IBloomFilterStorage {
        builder.Services.Replace(ServiceDescriptor.Singleton<IBloomFilterStorage, TStorage>());
        return builder;
    }

    public static BloomFilterBuilder AddAutoSave(this BloomFilterBuilder builder) {
        builder.Services.AddHostedService<BloomFilterAutoSaveService>();
        return builder;
    }
}