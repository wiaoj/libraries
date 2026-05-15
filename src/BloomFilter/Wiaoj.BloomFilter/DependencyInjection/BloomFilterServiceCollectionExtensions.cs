using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.ObjectPool.Extensions;

#pragma warning disable IDE0130
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register Bloom Filter services.
/// </summary>
public static class BloomFilterServiceCollectionExtensions {
    /// <summary>
    /// Adds Bloom Filter infrastructure and services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="setupAction">An optional action to configure the Bloom Filter builder.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddBloomFilter(
    this IServiceCollection services,
    Action<IBloomFilterBuilder>? setupAction = null) {

        services.AddOptions<BloomFilterOptions>()
                .BindConfiguration(BloomFilterOptions.SectionName);

        BloomFilterBuilder builder = new(services);
        setupAction?.Invoke(builder);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // Core
        services.TryAddSingleton<IBloomFilterConfigurationFactory, BloomFilterConfigurationFactory>();
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