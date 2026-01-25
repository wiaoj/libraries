namespace Wiaoj.BloomFilter;

public interface IBloomFilterService {
    // Tüm aktif filtrelerin doluluk oranlarını (stats) getirir
    ValueTask<IReadOnlyDictionary<FilterName, BloomFilterStats>> GetAllStatsAsync(CancellationToken ct = default);
    ValueTask<BloomFilterDetailedStats> GetDetailedStatsAsync(FilterName name);
    // Belirli bir filtreyi hem RAM'den hem storage'dan tamamen siler
    ValueTask DeleteFilterAsync(FilterName name, CancellationToken ct = default);

    // Tüm filtreleri zorla kaydeder (Flush)
    ValueTask SaveAllAsync(CancellationToken ct = default);

    // Belirli bir filtreyi storage'dan tekrar yükler (Sync)
    ValueTask ReloadFilterAsync(FilterName name, CancellationToken ct = default);
}
public sealed record BloomFilterStats(
    string Name,
    long ExpectedItems,
    double ConfiguredErrorRate,
    long SizeInBits,
    int HashFunctions,
    long SetBitsCount,
    double FillRatio,
    bool IsHealthy
); 

public sealed record BloomFilterDetailedStats(
    string Name,
    long TotalBits,
    long SetBits,
    double FillRatio,
    int HashFunctions,
    double CurrentFalsePositiveProbability,
    long MemoryUsageBytes
);
