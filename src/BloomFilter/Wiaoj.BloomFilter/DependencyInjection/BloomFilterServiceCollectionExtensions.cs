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

        // Core Services
        services.TryAddSingleton<IBloomFilterRegistry, BloomFilterRegistry>();
        services.TryAddSingleton<BloomFilterFactory>();
        services.TryAddSingleton<IBloomFilterService, BloomFilterService>();
        services.TryAddSingleton<IBloomFilterSeeder, BloomFilterSeeder>();
        services.TryAddSingleton<IBloomFilterStorage, FileSystemBloomFilterStorage>();

        // Background Services
        services.AddHostedService<BloomFilterAutoSaveService>();
        services.AddHostedService<BloomFilterWarmUpService>();

        services.AddObjectPool<MemoryStream>(
            factory: () => new MemoryStream(),
            resetter: ms => { ms.SetLength(0); return true; }
        );

        return services;
    }
}