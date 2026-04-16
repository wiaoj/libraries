using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Numerics;
using Wiaoj.BloomFilter.Advanced;
using Wiaoj.BloomFilter.Seeder;
using Wiaoj.ObjectPool;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Internal;

internal sealed class BloomFilterFactory(
    IOptionsMonitor<BloomFilterOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    IEnumerable<IAutoBloomFilterSeeder> autoSeeders,
    TimeProvider timeProvider,
    IObjectPool<MemoryStream> memoryStreamPool,
    IBloomFilterStorage? storage = null) {

    public async Task<IPersistentBloomFilter> Create(string filterNameStr, CancellationToken cancellationToken = default) {
        FilterName name = FilterName.Parse(filterNameStr);
        var currentOptions = optionsMonitor.CurrentValue;

        if(!currentOptions.Filters.TryGetValue(name.Value, out FilterDefinition? definition)) {
            InvalidOperationException ex = new($"Filter configuration for '{name}' not found.");
            loggerFactory.CreateLogger<BloomFilterFactory>().LogError(ex, "Configuration missing.");
            throw ex;
        }

        // Context (Tüm filtre tiplerinin ihtiyaç duyduğu ortak bağımlılıklar)
        BloomFilterContext context = new(
            storage,
            memoryStreamPool,
            loggerFactory.CreateLogger(name.Value),
            currentOptions,
            timeProvider
        );

        // BloomFilter Configuration
        BloomFilterConfiguration config = new(
            name,
            definition.ExpectedItems,
            Percentage.FromDouble(definition.ErrorRate),
            currentOptions.Performance.GlobalHashSeed
        );

        // Filtreyi Tipe Göre Factory Et
        IPersistentBloomFilter filter = definition.Type switch {
            BloomFilterType.Scalable => new ScalableBloomFilter(config, context, (GrowthRate)definition.GrowthRate),

            BloomFilterType.Rotating => new RotatingBloomFilter(config, context, definition.WindowSize, definition.ShardCount),

            _ => CreateDefaultFilter(config, context, currentOptions)
        };

        // Yükleme ve Hata Yönetimi
        try {
            await filter.ReloadAsync(cancellationToken);
        }
        catch(Exception ex) {
            loggerFactory.CreateLogger<BloomFilterFactory>().LogError(ex, "Load failed for '{Name}'. Resetting...", name);

            if(storage != null) {
                await storage.DeleteAsync(name.Value, cancellationToken);
            }

            if(currentOptions.Lifecycle.AutoReseed) {
                _ = Task.Run(() => TriggerAutoReseedAsync(filter, name, CancellationToken.None), CancellationToken.None);
            }
        }

        return filter;
    }

    private static IPersistentBloomFilter CreateDefaultFilter(BloomFilterConfiguration config, BloomFilterContext context, BloomFilterOptions options) {
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

    private async Task TriggerAutoReseedAsync(IPersistentBloomFilter filter, FilterName name, CancellationToken ct) {
        List<IAutoBloomFilterSeeder> matchingSeeders = autoSeeders.Where(s => s.FilterName == name).ToList();
        if(matchingSeeders.Count > 0) {
            var tasks = matchingSeeders.Select(s => s.SeedAsync(filter, ct));
            await Task.WhenAll(tasks);
            await filter.SaveAsync(ct);
        }
    }
}