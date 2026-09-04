
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.BloomFilter.Engine;
/// <summary>
/// A partition-based Bloom Filter implementation that shards data across multiple internal filters.
/// Requires at least 2 shards to partition keys and avoid Large Object Heap (LOH) allocations.
/// </summary>
internal sealed class ShardedBloomFilter : BloomFilterBase {
    private readonly InMemoryBloomFilter[] _shards;
    private readonly int _shardCount;
    private readonly int _shardMask;

    /// <inheritdoc/>
    public override FilterName Name => this.Configuration.Name;

    /// <inheritdoc/>
    public override bool IsDirty => this._shards.Any(s => s.IsDirty);

    /// <inheritdoc/>
    public override BloomFilterConfiguration Configuration { get; }

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
        Preca.ThrowIfNotPowerOfTwo(config.ShardCount, () => new ArgumentException("Shard count must be a power of 2.", nameof(config)));

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
    public override bool Add(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        uint shardIndex = (uint)(h1 & (ulong)this._shardMask);
        return this._shards[shardIndex].AddWithHashes(h1, h2);
    }

    /// <inheritdoc/>
    public override bool Contains(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        uint shardIndex = (uint)(h1 & (ulong)this._shardMask);
        return this._shards[shardIndex].ContainsWithHashes(h1, h2);
    }

    /// <inheritdoc/>
    public override long GetPopCount() {
        ThrowIfDisposed();

        long total = 0;
        for(int i = 0; i < this._shards.Length; i++) {
            total += this._shards[i].GetPopCount();
        }
        return total;
    }

    /// <inheritdoc/>
    public override async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        if(!this.IsDirty) return;

        IEnumerable<InMemoryBloomFilter> dirtyShards = this._shards.Where(s => s.IsDirty);

        await Parallel.ForEachAsync(dirtyShards, cancellationToken, async (shard, token) => {
            await shard.SaveAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        await Parallel.ForEachAsync(this._shards, cancellationToken, async (shard, token) => {
            await shard.ReloadAsync(token).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override void DisposeCore() {
        for(int i = 0; i < this._shards.Length; i++) {
            this._shards[i].Dispose();
        }
    }
}