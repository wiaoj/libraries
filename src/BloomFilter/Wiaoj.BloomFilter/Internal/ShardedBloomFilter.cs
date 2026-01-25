using Microsoft.Extensions.Logging;
using System.IO.Hashing;
using System.Text;

namespace Wiaoj.BloomFilter.Internal; 
internal sealed class ShardedBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly InMemoryBloomFilter[] _shards;
    private readonly int _shardCount;
    private readonly int _shardMask;

    public string Name => this.Configuration.Name;
    public bool IsDirty => this._shards.Any(s => s.IsDirty);

    public BloomFilterConfiguration Configuration { get; }

    public ShardedBloomFilter(BloomFilterConfiguration config,
                              IBloomFilterStorage? storage,
                              ILoggerFactory loggerFactory,
                              BloomFilterOptions options,
                              TimeProvider timeProvider) {

        this.Configuration = config;
        this._shardCount = config.ShardCount;

        // Shard sayısı 2'nin kuvveti olduğu için (örn: 16 -> 10000)
        // Maske 1 eksiğidir (örn: 15 -> 01111)
        // hash % 16 yerine hash & 15 yapabiliriz. Bu işlemcide çok daha hızlıdır.
        this._shardMask = _shardCount - 1;

        this._shards = new InMemoryBloomFilter[_shardCount];
        long itemsPerShard = (long)Math.Ceiling((double)config.ExpectedItems / _shardCount);

        for(int i = 0; i < _shardCount; i++) {
            var shardName = $"{config.Name}_s{i}";
            var shardConfig = new BloomFilterConfiguration(shardName, itemsPerShard, config.ErrorRate, config.HashSeed);

            _shards[i] = new InMemoryBloomFilter(shardConfig,
                                                 storage,
                                                 loggerFactory.CreateLogger<InMemoryBloomFilter>(),
                                                 options,
                                                 timeProvider);
        }
    }

    public bool Add(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, Configuration.HashSeed);

        // DİNAMİK VE HIZLI SHARD SEÇİMİ
        // (hash % count) yerine (hash & mask) kullanıyoruz.
        // Örnek: ShardCount=8, Mask=7 (111).
        uint shardIndex = (uint)(hash & (ulong)_shardMask);
        return this._shards[shardIndex].Add(item);
    }

    public bool Contains(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, Configuration.HashSeed);
        uint shardIndex = (uint)(hash & (ulong)_shardMask);
        return this._shards[shardIndex].Contains(item);
    }

    public long GetPopCount() {
        return this._shards.Sum(s => s.GetPopCount());
    }

    public async ValueTask SaveAsync(CancellationToken ct = default) {
        if(!IsDirty) return;

        // Sadece kirli olan shard'ları bul
        var dirtyShards = _shards.Where(s => s.IsDirty).ToList();

        // Aynı anda en fazla 4 dosya yaz (Disk I/O darboğazını engeller)
        var parallelOptions = new ParallelOptions {
            MaxDegreeOfParallelism = 4, // Disk hızına göre artırılabilir (SSD ise 8-16)
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(dirtyShards, parallelOptions, async (shard, token) => {
            await shard.SaveAsync(token);
        });
    }

    public async ValueTask ReloadAsync(CancellationToken ct = default) {
        var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct };

        await Parallel.ForEachAsync(_shards, parallelOptions, async (shard, token) => {
            await shard.ReloadAsync(token);
        });
    }

    public void Dispose() {
        foreach(var shard in _shards) shard.Dispose();
    }

    public bool Add(ReadOnlySpan<char> item) {
        return Add(Encoding.UTF8.GetBytes(item.ToString()));
    }

    public bool Contains(ReadOnlySpan<char> item) {
        return Contains(Encoding.UTF8.GetBytes(item.ToString()));
    }
}