using System.IO.Hashing;
using System.Numerics;
using System.Text;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter;

/// <summary>
/// A partition-based Bloom Filter implementation that shards data across multiple internal filters.
/// Requires at least 2 shards to partition keys and avoid Large Object Heap (LOH) allocations.
/// </summary>
internal sealed class ShardedBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly InMemoryBloomFilter[] _shards;
    private readonly int _shardCount;
    private readonly int _shardMask;
    private readonly StripedLock<int> _stripedIoLock = new(stripes: 128);

    /// <inheritdoc/>
    public string Name => this.Configuration.Name.Value;

    /// <inheritdoc/>
    public bool IsDirty => this._shards.Any(s => s.IsDirty);

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardedBloomFilter"/> class.
    /// </summary>
    /// <param name="config">The filter configuration. ShardCount must be at least 2.</param>
    /// <param name="context">The shared bloom filter context.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="BloomFilterConfiguration.ShardCount"/> is less than 2.</exception>
    /// <exception cref="ArgumentException">Thrown when <see cref="BloomFilterConfiguration.ShardCount"/> is not a power of 2.</exception>
    public ShardedBloomFilter(BloomFilterConfiguration config, BloomFilterContext context) {
        Preca.ThrowIfNull(config, nameof(config));
        Preca.ThrowIfNull(context, nameof(context));
        Preca.ThrowIfLessThan(config.ShardCount, 2, () => new ArgumentOutOfRangeException(nameof(config), "Shard count must be at least 2 for a sharded filter."));

        if(!BitOperations.IsPow2(config.ShardCount)) {
            throw new ArgumentException("Shard count must be a power of 2.", nameof(config));
        }

        this.Configuration = config;
        this._shardCount = config.ShardCount;
        this._shardMask = this._shardCount - 1;

        this._shards = new InMemoryBloomFilter[this._shardCount];
        long itemsPerShard = (long)Math.Ceiling((double)config.ExpectedItems / this._shardCount);

        for(int i = 0; i < this._shardCount; i++) {
            FilterName shardName = FilterName.Parse($"{config.Name.Value}_s{i}");
            BloomFilterConfiguration shardConfig = context.ConfigFactory.Create(
                shardName,
                itemsPerShard,
                config.ErrorRate,
                config.HashSeed);

            this._shards[i] = new InMemoryBloomFilter(shardConfig, context);
        }
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, this.Configuration.HashSeed);
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Add(item);
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        ulong hash = XxHash3.HashToUInt64(item, this.Configuration.HashSeed);
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Contains(item);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        if(maxBytes <= 256) {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(item, buffer);
            return Add(buffer[..written]);
        }
        return Add(Encoding.UTF8.GetBytes(item.ToString()));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        if(maxBytes <= 256) {
            Span<byte> buffer = stackalloc byte[maxBytes];
            int written = Encoding.UTF8.GetBytes(item, buffer);
            return Contains(buffer[..written]);
        }
        return Contains(Encoding.UTF8.GetBytes(item.ToString()));
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        long total = 0;
        for(int i = 0; i < this._shards.Length; i++) {
            total += this._shards[i].GetPopCount();
        }
        return total;
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        if(!this.IsDirty) return;

        var dirtyShards = this._shards.Select((s, idx) => (s, idx)).Where(x => x.s.IsDirty);

        await Parallel.ForEachAsync(dirtyShards, cancellationToken, async (shard, token) => {
            using(await this._stripedIoLock.LockAsync(shard.idx, token).ConfigureAwait(false)) {
                await shard.s.SaveAsync(token).ConfigureAwait(false);
            }
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        await Parallel.ForEachAsync(this._shards, cancellationToken, async (shard, token) => {
            await shard.ReloadAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose() {
        for(int i = 0; i < this._shards.Length; i++) {
            this._shards[i].Dispose();
        }
    }
}