using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.BloomFilter.Engine;

internal sealed class BloomFilterService(
    IServiceProvider serviceProvider,
    IBloomFilterRegistry registry,
    IOptions<BloomFilterOptions> options,
    ILogger<BloomFilterService> logger,
    IBloomFilterStorage? storage = null) : IBloomFilterService {

    public ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>> GetAllStatsAsync(CancellationToken ct = default) {
        Dictionary<FilterName, BloomFilterStats> statsMap = [];

        foreach(string key in options.Value.Filters.Keys) {
            FilterName name = FilterName.Parse(key);
            FilterDefinition definition = options.Value.Filters[key];

            IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(key);

            long setBits = filter.GetPopCount();
            long sizeInBits = filter.Configuration.SizeInBits;
            double ratio = sizeInBits > 0 ? (double)setBits / sizeInBits : 0.0;

            bool isHealthy = definition.Type switch {
                BloomFilterType.Scalable => ratio <= (definition.SaturationThreshold > 0 ? definition.SaturationThreshold : 0.50),
                _ => Math.Pow(ratio, filter.Configuration.HashFunctionCount) <= definition.ErrorRate
            };

            statsMap[name] = new BloomFilterStats(
                filter.Name, definition.ExpectedItems, definition.ErrorRate,
                sizeInBits, filter.Configuration.HashFunctionCount,
                setBits, ratio, isHealthy
            );
        }
        return new ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>>(statsMap);
    }

    public ValueTask<BloomFilterDetailedStats> GetDetailedStatsAsync(FilterName name) {
        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(name.Value);
        long setBits = filter.GetPopCount();
        long m = filter.Configuration.SizeInBits;
        int k = filter.Configuration.HashFunctionCount;
        double fillRatio = (double)setBits / m;
        double currentFpProb = Math.Pow(fillRatio, k);

        if(fillRatio > 0.5) logger.LogSaturationWarning(name, fillRatio, currentFpProb);

        return new ValueTask<BloomFilterDetailedStats>(new BloomFilterDetailedStats(
            name, m, setBits, fillRatio, k, currentFpProb, BloomMath.BitsToBytes(m)));
    }

    public async ValueTask SaveAllAsync(CancellationToken ct = default) {
        logger.LogGlobalSaveTriggered();
        foreach(IPersistentBloomFilter filter in registry.GetAll()) {
            if(filter.IsDirty)
                await filter.SaveAsync(ct);
        }
    }

    public async ValueTask ReloadFilterAsync(FilterName name, CancellationToken ct = default) {
        IPersistentBloomFilter filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(name.Value);
        await filter.ReloadAsync(ct);
    }

    public async ValueTask DeleteFilterAsync(FilterName name, CancellationToken ct = default) {
        if(storage != null)
            await storage.DeleteAsync(name.Value, ct);
        logger.LogFilterDeleted(name);
    }
}