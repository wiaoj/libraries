using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Numerics;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.ObjectPool;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter;

/// <summary>
/// Internal factory responsible for instantiating, hydrating, and recovering persistent Bloom Filter instances.
/// </summary>
internal sealed class BloomFilterFactory(
    IBloomFilterConfigurationFactory configFactory,
    IOptionsMonitor<BloomFilterOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    IEnumerable<IAutoBloomFilterSeeder> autoSeeders,
    TimeProvider timeProvider,
    IObjectPool<MemoryStream> memoryStreamPool,
    IHostApplicationLifetime? hostLifetime = null,
    IBloomFilterStorage? storage = null) {

    private readonly ILogger _logger = loggerFactory.CreateLogger<BloomFilterFactory>();

    /// <summary>
    /// Creates and hydrates a Bloom Filter instance by name.
    /// </summary>
    public async Task<IPersistentBloomFilter> Create(FilterName name, CancellationToken cancellationToken = default) {
        if(name.IsEmpty) {
            throw new ArgumentException("Filter name cannot be empty.", nameof(name));
        }

        BloomFilterOptions currentOptions = optionsMonitor.CurrentValue;

        if(!currentOptions.Filters.TryGetValue(name.Value, out FilterDefinition? definition)) {
            InvalidOperationException ex = new($"Filter configuration for '{name.Value}' was not found in options.");
            this._logger.LogError(ex, "Configuration missing for Bloom Filter '{Name}'.", name.Value);
            throw ex;
        }

        BloomFilterContext context = new(
            storage,
            memoryStreamPool,
            loggerFactory.CreateLogger(name.Value),
            currentOptions,
            timeProvider,
            configFactory
        );

        BloomFilterConfiguration config = currentOptions.DefaultHashSeed.HasValue
            ? configFactory.Create(name, definition.ExpectedItems, definition.ErrorRate, currentOptions.DefaultHashSeed.Value)
            : configFactory.Create(name, definition.ExpectedItems, definition.ErrorRate);

        IPersistentBloomFilter filter = definition.Type switch {
            BloomFilterType.Scalable => new ScalableBloomFilter(config,
                                                                context,
                                                                (GrowthRate)definition.GrowthRate,
                                                                Percentage.FromDouble(definition.SaturationThreshold)),
            BloomFilterType.Rotating => new RotatingBloomFilter(config,
                                                                context,
                                                                definition.WindowSize,
                                                                definition.ShardCount),
            _ => CreateDefaultFilter(config, context, currentOptions)
        };

        // Hydration and Failure Recovery
        try {
            await filter.ReloadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch(Exception ex) {
            this._logger.LogError(ex, "Failed to hydrate Bloom Filter '{Name}' from storage. Reinitializing clean filter.", name.Value);

            if(storage != null) {
                try {
                    await storage.DeleteAsync(name, cancellationToken).ConfigureAwait(false);
                }
                catch(Exception delEx) {
                    this._logger.LogWarning(delEx, "Failed to delete corrupted storage files for '{Name}'.", name.Value);
                }
            }

            if(currentOptions.Lifecycle.AutoReseed) {
                // Execute managed background reseed linked to host shutdown token
                CancellationToken stoppingToken = hostLifetime?.ApplicationStopping ?? CancellationToken.None;
                _ = Task.Run(async () => await ExecuteManagedReseedAsync(filter, name, stoppingToken).ConfigureAwait(false), stoppingToken);
            }
        }

        return filter;
    }

    private static IPersistentBloomFilter CreateDefaultFilter(
        BloomFilterConfiguration config,
        BloomFilterContext context,
        BloomFilterOptions options) {

        long totalBytes = (config.SizeInBits + 7) / 8;
        int calculatedShards = 1;

        if(totalBytes > options.Lifecycle.ShardingThresholdBytes) {
            double ratio = (double)totalBytes / options.Lifecycle.ShardingThresholdBytes;
            int needed = (int)Math.Ceiling(ratio);
            calculatedShards = (int)BitOperations.RoundUpToPowerOf2((uint)needed);
        }

        return calculatedShards > 1
            ? new ShardedBloomFilter(config.WithShardCount(calculatedShards), context)
            : new InMemoryBloomFilter(config, context);
    }

    private async Task ExecuteManagedReseedAsync(IPersistentBloomFilter filter, FilterName name, CancellationToken ct) {
        try {
            List<IAutoBloomFilterSeeder> matchingSeeders = autoSeeders.Where(s => s.FilterName == name).ToList();
            if(matchingSeeders.Count == 0) {
                return;
            }

            this._logger.LogInformation("Triggering automatic reseeding for Bloom Filter '{Name}'.", name.Value);

            foreach(IAutoBloomFilterSeeder seeder in matchingSeeders) {
                ct.ThrowIfCancellationRequested();
                await seeder.SeedAsync(filter, ct).ConfigureAwait(false);
            }

            await filter.SaveAsync(ct).ConfigureAwait(false);
            this._logger.LogInformation("Automatic reseeding completed successfully for '{Name}'.", name.Value);
        }
        catch(OperationCanceledException) {
            this._logger.LogWarning("Automatic reseeding for '{Name}' was aborted due to application shutdown.", name.Value);
        }
        catch(Exception ex) {
            this._logger.LogError(ex, "Critical failure during automatic reseeding of '{Name}'.", name.Value);
        }
    }
}