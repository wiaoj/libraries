using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Diagnostics;

namespace Wiaoj.BloomFilter.Internal; 
internal sealed class BloomFilterService : IBloomFilterService {
    private readonly IBloomFilterProvider _provider;
    private readonly IBloomFilterLifecycleManager _lifecycleManager;
    private readonly IBloomFilterStorage? _storage;
    private readonly BloomFilterOptions _options;
    private readonly ILogger<BloomFilterService> _logger;

    public BloomFilterService(
        IBloomFilterProvider provider,
        IBloomFilterLifecycleManager lifecycleManager,
        IOptions<BloomFilterOptions> options,
        ILogger<BloomFilterService> logger,
        IBloomFilterStorage? storage = null) {
        this._provider = provider; this._lifecycleManager = lifecycleManager;
        this._storage = storage;
        this._options = options.Value;
        this._logger = logger;
    }

    public async ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>> GetAllStatsAsync(CancellationToken ct = default) {
        Dictionary<FilterName, BloomFilterStats> statsMap = new();

        foreach(var key in this._options.Filters.Keys) {
            FilterName name = FilterName.Parse(key);
            var definition = this._options.Filters[key];

            // Filtreyi RAM'e yükle (eğer yüklenmemişse)
            var filter = await this._provider.GetAsync(name);

            long setBits = filter.GetPopCount();
            double ratio = (double)setBits / filter.Configuration.SizeInBits;

            statsMap[name] = new BloomFilterStats(
                filter.Name,
                definition.ExpectedItems, // Tanımdan gelen kapasite
                definition.ErrorRate,     // Tanımdan gelen hata oranı
                filter.Configuration.SizeInBits,
                filter.Configuration.HashFunctionCount,
                setBits,
                ratio,
                ratio < 0.55
            );
        }
        return statsMap;
    }

    public async ValueTask<BloomFilterDetailedStats> GetDetailedStatsAsync(FilterName name) {
        var filter = await this._provider.GetAsync(name);

        // We need an internal method in PooledBitArray using BitOperations.PopCount
        long setBits = filter.GetPopCount();
        long m = filter.Configuration.SizeInBits;
        int k = filter.Configuration.HashFunctionCount;

        double fillRatio = (double)setBits / m;

        // The probability of a false positive is roughly (1 - e^(-kn/m))^k
        // Which is equivalent to (FillRatio)^k
        double currentFpProb = Math.Pow(fillRatio, k);

        if(fillRatio > 0.5) {
            this._logger.LogSaturationWarning(name, fillRatio, currentFpProb);
        }

        return new BloomFilterDetailedStats(
            name, m, setBits, fillRatio, k, currentFpProb, (m + 7) / 8
        );
    }
    public async ValueTask SaveAllAsync(CancellationToken ct = default) {
        this._logger.LogInformation("Global save triggered for all Bloom Filters.");
        // Provider içindeki SaveAllDirtyAsync metodunu tetikle
        await this._lifecycleManager.SaveAllDirtyAsync(ct);
    }

    public async ValueTask ReloadFilterAsync(FilterName name, CancellationToken ct = default) {
        var filter = await this._provider.GetAsync(name);
        if(filter is IPersistentBloomFilter persistent) {
            await persistent.ReloadAsync(ct);
        }
    }

    public async ValueTask DeleteFilterAsync(FilterName name, CancellationToken ct = default) {
        // 1. Storage'dan sil
        if(this._storage != null) {
            // IBloomFilterStorage arayüzüne 'DeleteAsync' eklenmeli
            await this._storage.DeleteAsync(name, ct);
        }

        // 2. RAM'den sil (Provider içindeki ConcurrentDictionary'den kaldır)
        // Bunun için Provider'a bir 'TryRemove(name)' metodu eklemek gerekir.
        this._logger.LogWarning("Filter '{Name}' has been deleted from storage and memory.", name);
    }
}