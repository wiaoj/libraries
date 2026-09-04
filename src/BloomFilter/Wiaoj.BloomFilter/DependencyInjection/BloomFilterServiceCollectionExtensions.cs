using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.BloomFilter.Storage;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extension methods for Bloom Filter registration.
/// </summary>
public static class BloomFilterServiceCollectionExtensions {
    /// <summary>
    /// Registers core Bloom Filter infrastructure.
    /// </summary>
    public static IServiceCollection AddBloomFilter(this IServiceCollection services) {
        return services.AddBloomFilter(_ => { });
    }

    /// <summary>
    /// Registers core Bloom Filter infrastructure and configures filters via builder.
    /// </summary>
    public static IServiceCollection AddBloomFilter(
        this IServiceCollection services,
        Action<IBloomFilterBuilder> setupAction) {

        services.AddOptions<BloomFilterOptions>();
        services.AddOptions<FileSystemStorageOptions>();

        // Safely bind IConfiguration if registered in DI container (Optional Binding)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<BloomFilterOptions>, OptionalConfigurationBinder>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<FileSystemStorageOptions>, OptionalFileSystemStorageConfigurationBinder>());

        BloomFilterBuilder builder = new(services);
        setupAction(builder);

        services.TryAddSingleton(TimeProvider.System);

        // Fallback logging for tests and standalone apps without explicit AddLogging()
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(NullLogger<>)));

        // Internal engine infrastructure
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

    private sealed class OptionalConfigurationBinder(IServiceProvider serviceProvider) : IConfigureOptions<BloomFilterOptions> {
        public void Configure(BloomFilterOptions options) {
            IConfiguration? configuration = serviceProvider.GetService<IConfiguration>();
            configuration?.GetSection(BloomFilterOptions.SectionName).Bind(options);
        }
    }

    private sealed class OptionalFileSystemStorageConfigurationBinder(IServiceProvider serviceProvider) : IConfigureOptions<FileSystemStorageOptions> {
        public void Configure(FileSystemStorageOptions options) {
            IConfiguration? configuration = serviceProvider.GetService<IConfiguration>();
            configuration?.GetSection(FileSystemStorageOptions.SectionName).Bind(options);
        }
    }
}