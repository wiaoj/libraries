using Microsoft.Extensions.Logging;
using System.IO.Hashing;
using System.Text;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool;
using Wiaoj.Serialization;

namespace Wiaoj.BloomFilter.Internal;

internal sealed class ShardedBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly InMemoryBloomFilter[] _shards;
    private readonly int _shardCount;
    private readonly int _shardMask;

    public string Name => this.Configuration.Name;
    public bool IsDirty => this._shards.Any(s => s.IsDirty);
    private readonly StripedLock<int> _stripedIoLock = new(stripes: 128);

    public BloomFilterConfiguration Configuration { get; }

    public ShardedBloomFilter(BloomFilterConfiguration config, BloomFilterContext context) {

        this.Configuration = config;
        this._shardCount = config.ShardCount;

        // Shard sayısı 2'nin kuvveti olduğu için (örn: 16 -> 10000)
        // Maske 1 eksiğidir (örn: 15 -> 01111)
        // hash % 16 yerine hash & 15 yapabiliriz. Bu işlemcide çok daha hızlıdır.
        this._shardMask = this._shardCount - 1;

        this._shards = new InMemoryBloomFilter[this._shardCount];
        long itemsPerShard = (long)Math.Ceiling((double)config.ExpectedItems / this._shardCount);

        for(int i = 0; i < this._shardCount; i++) {
            string shardName = $"{config.Name}_s{i}";
            var shardConfig = new BloomFilterConfiguration(shardName, itemsPerShard, config.ErrorRate, config.HashSeed);

            this._shards[i] = new InMemoryBloomFilter(shardConfig, context);
        }
    }

    public bool Add(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, this.Configuration.HashSeed);

        // DİNAMİK VE HIZLI SHARD SEÇİMİ
        // (hash % count) yerine (hash & mask) kullanıyoruz.
        // Örnek: ShardCount=8, Mask=7 (111).
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Add(item);
    }

    public bool Contains(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, this.Configuration.HashSeed);
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Contains(item);
    }

    public long GetPopCount() {
        return this._shards.Sum(s => s.GetPopCount());
    }

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        if(!this.IsDirty) return;

        // Sadece kirli olan shard'ları bul
        var dirtyShards = this._shards.Where(s => s.IsDirty).Select((s, idx) => (s, idx));

        await Parallel.ForEachAsync(dirtyShards, cancellationToken, async (shard, token) => {
            using(await this._stripedIoLock.LockAsync(shard.idx, token)) {
                await shard.s.SaveAsync(token);
            }
        });
    }

    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        await Parallel.ForEachAsync(this._shards, cancellationToken, async (shard, token) => {
            await shard.ReloadAsync(token);
        });
    }

    public void Dispose() {
        foreach(var shard in this._shards) shard.Dispose();
    }

    public bool Add(ReadOnlySpan<char> item) {
        return Add(Encoding.UTF8.GetBytes(item.ToString()));
    }

    public bool Contains(ReadOnlySpan<char> item) {
        return Contains(Encoding.UTF8.GetBytes(item.ToString()));
    }
}