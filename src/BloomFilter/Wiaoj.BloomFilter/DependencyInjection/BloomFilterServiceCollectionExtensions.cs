using Microsoft.Extensions.DependencyInjection.Extensions;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Hosting;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.ObjectPool.Extensions;
using Wiaoj.Serialization.DependencyInjection;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // Namespace does not match folder structure
public static class BloomFilterServiceCollectionExtensions {

    /// <summary>
    /// Adds Wiaoj Bloom Filter services to the DI container.
    /// Automatically binds configuration from the "BloomFilter" section in appsettings.json.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="setupAction">Optional builder action for code-based configuration.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBloomFilter(
        this IServiceCollection services,
        Action<BloomFilterBuilder>? setupAction = null) {
        // 1. Bind Options (Otomatik)
        // Nuget Paketi Gerekli: Microsoft.Extensions.Options.ConfigurationExtensions
        services.AddOptions<BloomFilterOptions>()
                .BindConfiguration("BloomFilter");
        //.ValidateDataAnnotations()
        //.ValidateOnStart();

        // 2. Create Builder & Execute User Code
        BloomFilterBuilder builder = new(services);
        setupAction?.Invoke(builder);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);

        // 3. Register Core Services
        services.TryAddSingleton<BloomFilterProvider>();
        services.TryAddSingleton<IBloomFilterProvider>(sp =>
            sp.GetRequiredService<BloomFilterProvider>());
        services.TryAddSingleton<IBloomFilterLifecycleManager>(sp =>
            sp.GetRequiredService<BloomFilterProvider>());

        services.TryAddSingleton<IBloomFilterService, BloomFilterService>();
        services.TryAddSingleton<IBloomFilterSeeder, BloomFilterSeeder>();

        // 4. Default Storage (File System) - If user didn't replace it via AddStorage
        services.TryAddSingleton<IBloomFilterStorage, FileSystemBloomFilterStorage>();

        // 5. Background Services
        services.AddHostedService<BloomFilterAutoSaveService>();
        services.AddHostedService<BloomFilterWarmUpService>();
        services.AddObjectPool<MemoryStream>(
            factory: () => new MemoryStream(),
            resetter: ms => {
                ms.SetLength(0);
                return true;
            }
        );

        services.AddWiaojSerializer(serializer => {
            serializer.UseMessagePack<InMemorySerializerKey>();
        });
        return services;
    }
}