using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Numerics;
using Wiaoj.BloomFilter.Advanced;
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

    public async Task<IPersistentBloomFilter> CreateAndLoadAsync(string filterNameStr, CancellationToken ct) {
        var name = FilterName.Parse(filterNameStr);
        var currentOptions = optionsMonitor.CurrentValue;

        if(!currentOptions.Filters.TryGetValue(name.Value, out FilterDefinition? definition)) {
            var ex = new InvalidOperationException($"Filter configuration for '{name}' not found.");
            loggerFactory.CreateLogger<BloomFilterFactory>().LogError(ex, "Configuration missing.");
            throw ex;
        }

        var config = new BloomFilterConfiguration(
            name,
            definition.ExpectedItems,
            Percentage.FromDouble(definition.ErrorRate),
            currentOptions.Performance.GlobalHashSeed
        );

        long totalBytes = (config.SizeInBits + 7) / 8;
        int calculatedShards = 1;

        if(totalBytes > currentOptions.Lifecycle.ShardingThresholdBytes) {
            double ratio = (double)totalBytes / currentOptions.Lifecycle.ShardingThresholdBytes;
            int needed = (int)Math.Ceiling(ratio);
            calculatedShards = (int)BitOperations.RoundUpToPowerOf2((uint)needed);
        }

        var finalConfig = config.WithShardCount(calculatedShards);
        var context = new BloomFilterContext(storage, memoryStreamPool, loggerFactory.CreateLogger(name.Value), currentOptions, timeProvider);

        IPersistentBloomFilter filter;

        if(definition.IsScalable) {
            filter = new ScalableBloomFilter(config, context, (GrowthRate)definition.GrowthRate);
        }
        else if(calculatedShards > 1) {
            filter = new ShardedBloomFilter(finalConfig, context);
        }
        else {
            filter = new InMemoryBloomFilter(finalConfig, context);
        }

        try {
            await filter.ReloadAsync(ct);
        }
        catch(Exception ex) {
            loggerFactory.CreateLogger<BloomFilterFactory>().LogError(ex, "Load failed for '{Name}'. Resetting...", name);

            if(storage != null) {
                await storage.DeleteAsync(name.Value, ct);
            }

            if(currentOptions.Lifecycle.AutoReseed) {
                _ = Task.Run(() => TriggerAutoReseedAsync(filter, name, CancellationToken.None), CancellationToken.None);
            }
        }

        return filter;
    }

    private async Task TriggerAutoReseedAsync(IPersistentBloomFilter filter, FilterName name, CancellationToken ct) {
        var matchingSeeders = autoSeeders.Where(s => s.FilterName == name).ToList();
        if(matchingSeeders.Count > 0) {
            var tasks = matchingSeeders.Select(s => s.SeedAsync(filter, ct));
            await Task.WhenAll(tasks);
            await filter.SaveAsync(ct);
        }
    }
}