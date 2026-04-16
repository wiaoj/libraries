using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.BloomFilter;
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
    Action<IBloomFilterBuilder>? setupAction = null) {

        services.AddOptions<BloomFilterOptions>()
                .BindConfiguration(BloomFilterOptions.SectionName);

        BloomFilterBuilder builder = new(services);
        setupAction?.Invoke(builder);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Core
        services.TryAddSingleton<IBloomFilterRegistry, BloomFilterRegistry>();
        services.TryAddSingleton<BloomFilterFactory>();
        services.TryAddSingleton<IBloomFilterService, BloomFilterService>();
        services.TryAddSingleton<IBloomFilterSeeder, BloomFilterSeeder>();
        services.TryAddSingleton<IBloomFilterStorage, FileSystemBloomFilterStorage>();

        services.AddObjectPool<MemoryStream>(
            factory: () => new MemoryStream(),
            resetter: ms => { ms.SetLength(0); return true; }
        );

        return services;
    }
}