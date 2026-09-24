using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IO;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.DependencyInjection;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.BloomFilter.Seeding;
using Wiaoj.BloomFilter.Storage;
using Wiaoj.Preconditions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Service collection extension methods for Bloom Filter registration.
/// </summary>
public static class BloomFilterServiceCollectionExtensions {
    /// <summary>
    /// Registers core Bloom Filter infrastructure and configures filters inside <paramref name="setupAction"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="setupAction">Registers filters and lifecycle features on the builder.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddBloomFilter(
        this IServiceCollection services,
        Action<IBloomFilterBuilder> setupAction) {
        Preca.ThrowIfNull(services);
        Preca.ThrowIfNull(setupAction);

        setupAction(services.AddBloomFilter());
        return services;
    }

    /// <summary>
    /// Registers core Bloom Filter infrastructure and returns a builder to register filters on.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The Bloom Filter builder.</returns>
    public static IBloomFilterBuilder AddBloomFilter(this IServiceCollection services) {
        Preca.ThrowIfNull(services);


        services.AddOptions<BloomFilterOptions>()
            .Validate(opts => {
                opts.Validate();
                return true;
            });
        services.AddOptions<FileSystemStorageOptions>();

        // Safely bind IConfiguration if registered in DI container (Optional Binding)
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<BloomFilterOptions>, OptionalConfigurationBinder>());
        services.TryAddEnumerable(ServiceDescriptor.Transient<IConfigureOptions<FileSystemStorageOptions>, OptionalFileSystemStorageConfigurationBinder>());

        BloomFilterBuilder builder = new(services);

        // Read when the options are resolved, so filters and lifecycle features added after this method returns
        // (fluent calls, or the setupAction overload) are included.
        services.Configure<BloomFilterOptions>(options => {
            options.Lifecycle.AutoSaveInterval = builder.Options.Lifecycle.AutoSaveInterval;
            options.Lifecycle.EnableIntegrityCheck = builder.Options.Lifecycle.EnableIntegrityCheck;
            options.Lifecycle.EnableWarmUp = builder.Options.Lifecycle.EnableWarmUp;
            options.Lifecycle.AutoReseed = builder.Options.Lifecycle.AutoReseed;
            options.Lifecycle.ShardingThresholdBytes = builder.Options.Lifecycle.ShardingThresholdBytes;
            options.Lifecycle.AutoResetOnMismatch = builder.Options.Lifecycle.AutoResetOnMismatch;

            foreach(var (filterName, filterDef) in builder.Options.Filters) {
                options.Filters[filterName] = filterDef;
            }
        });

        services.TryAddSingleton(TimeProvider.System);

        // Fallback logging for tests and standalone apps without explicit AddLogging()
        services.TryAddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(NullLogger<>)));

        // Internal engine infrastructure
        services.TryAddSingleton<IBloomFilterConfigurationFactory, BloomFilterConfigurationFactory>();
        services.TryAddSingleton<IBloomFilterRegistry, BloomFilterRegistry>();
        services.TryAddSingleton<BloomFilterFactory>(sp => new BloomFilterFactory(
            sp.GetRequiredService<IBloomFilterConfigurationFactory>(),
            sp.GetRequiredService<IOptionsMonitor<BloomFilterOptions>>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetServices<IAutoBloomFilterSeeder>(),
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<RecyclableMemoryStreamManager>(),
            sp.GetService<IBloomFilterStorage>()));
        services.TryAddSingleton<IBloomFilterService, BloomFilterService>();
        services.TryAddSingleton<IBloomFilterSeeder, BloomFilterSeeder>();

        services.TryAddSingleton<RecyclableMemoryStreamManager>();

        return builder;
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