using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.ObjectPool.Extensions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

public static class BloomFilterServiceCollectionExtensions {

    public static IServiceCollection AddBloomFilter(
        this IServiceCollection services,
        Action<BloomFilterBuilder>? setupAction = null) {

        services.AddOptions<BloomFilterOptions>()
                .BindConfiguration(BloomFilterOptions.SectionName);

        BloomFilterBuilder builder = new(services);
        setupAction?.Invoke(builder);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Core Services (Registry & Factory)
        services.TryAddSingleton<IBloomFilterRegistry, BloomFilterRegistry>();
        services.TryAddSingleton<BloomFilterFactory>();

        services.TryAddSingleton<IBloomFilterService, BloomFilterService>();
        services.TryAddSingleton<IBloomFilterSeeder, BloomFilterSeeder>();

        // Varsayılan appsettings içindeki filtreleri ayağa kaldırmak için otomatik Keyed kayıt
        services.AddSingleton<IConfigureOptions<BloomFilterOptions>, BloomFilterOptionsSetup>();

        services.TryAddSingleton<IBloomFilterStorage, FileSystemBloomFilterStorage>();

        services.AddHostedService<BloomFilterAutoSaveService>();
        services.AddHostedService<BloomFilterWarmUpService>();

        services.AddObjectPool<MemoryStream>(
            factory: () => new MemoryStream(),
            resetter: ms => { ms.SetLength(0); return true; }
        );

        return services;
    }
}

// appsettings.json'dan gelenleri Keyed Service olarak otomatik kaydetmek için PostConfigure hilesi
internal class BloomFilterOptionsSetup(IServiceCollection services) : IConfigureOptions<BloomFilterOptions> {
    public void Configure(BloomFilterOptions options) {
        foreach(var key in options.Filters.Keys) {
            services.AddKeyedSingleton<IBloomFilter>(key, (sp, k) => {
                var factory = sp.GetRequiredService<BloomFilterFactory>();
                var registry = sp.GetRequiredService<IBloomFilterRegistry>();
                var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                return new LazyBloomFilterProxy((string)k, factory, registry, loggerFactory);
            });
            services.AddKeyedSingleton<IPersistentBloomFilter>(key, (sp, k) =>
                (IPersistentBloomFilter)sp.GetRequiredKeyedService<IBloomFilter>(k));
        }
    }
}