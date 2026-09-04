
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
    private readonly StripedLock<int> _stripedIoLock = new(stripes: 128);

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

        ulong hash = XxHash3.Compute(item, this.Configuration.HashSeed).Value;
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Add(item);
    }

    /// <inheritdoc/>
    public override bool Contains(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        ulong hash = XxHash3.Compute(item, this.Configuration.HashSeed).Value;
        uint shardIndex = (uint)(hash & (ulong)this._shardMask);
        return this._shards[shardIndex].Contains(item);
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

        IEnumerable<(InMemoryBloomFilter s, int idx)> dirtyShards = this._shards.Select((s, idx) => (s, idx)).Where(x => x.s.IsDirty);

        await Parallel.ForEachAsync(dirtyShards, cancellationToken, async (shard, token) => {
            using(await this._stripedIoLock.LockAsync(shard.idx, token).ConfigureAwait(false)) {
                await shard.s.SaveAsync(token).ConfigureAwait(false);
            }
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