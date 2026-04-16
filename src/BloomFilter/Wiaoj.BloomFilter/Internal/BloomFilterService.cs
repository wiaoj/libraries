using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.BloomFilter.Internal;

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
            var definition = options.Value.Filters[key];

            var filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(key);

            long setBits = filter.GetPopCount();
            double ratio = (double)setBits / filter.Configuration.SizeInBits;

            statsMap[name] = new BloomFilterStats(
                filter.Name, definition.ExpectedItems, definition.ErrorRate,
                filter.Configuration.SizeInBits, filter.Configuration.HashFunctionCount,
                setBits, ratio, ratio < 0.55
            );
        }
        return new ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>>(statsMap);
    }

    public ValueTask<BloomFilterDetailedStats> GetDetailedStatsAsync(FilterName name) {
        var filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(name.Value);
        long setBits = filter.GetPopCount();
        long m = filter.Configuration.SizeInBits;
        int k = filter.Configuration.HashFunctionCount;
        double fillRatio = (double)setBits / m;
        double currentFpProb = Math.Pow(fillRatio, k);

        if(fillRatio > 0.5) logger.LogSaturationWarning(name, fillRatio, currentFpProb);

        return new ValueTask<BloomFilterDetailedStats>(new BloomFilterDetailedStats(
            name, m, setBits, fillRatio, k, currentFpProb, (m + 7) / 8));
    }

    public async ValueTask SaveAllAsync(CancellationToken ct = default) {
        logger.LogInformation("Global save triggered for all Bloom Filters.");
        foreach(var filter in registry.GetAll()) {
            if(filter.IsDirty) await filter.SaveAsync(ct);
        }
    }

    public async ValueTask ReloadFilterAsync(FilterName name, CancellationToken ct = default) {
        var filter = serviceProvider.GetRequiredKeyedService<IPersistentBloomFilter>(name.Value);
        await filter.ReloadAsync(ct);
    }

    public async ValueTask DeleteFilterAsync(FilterName name, CancellationToken ct = default) {
        if(storage != null) await storage.DeleteAsync(name.Value, ct);
        logger.LogWarning("Filter '{Name}' has been deleted from storage.", name);
    }
}