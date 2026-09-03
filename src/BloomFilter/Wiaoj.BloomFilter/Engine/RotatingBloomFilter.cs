using System.Numerics;
using System.Text;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter;

/// <summary>
/// A sliding-window Bloom Filter that rotates underlying time shards based on a configured Time-To-Live (TTL).
/// Drops expired time windows while maintaining queryability across active sliding windows.
/// </summary>
internal sealed class RotatingBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly TimeSpan _shardDuration;
    private readonly DisposeState _disposeState = new();

    private Shard[] _shards;

    /// <inheritdoc/>
    public string Name => this.Configuration.Name.Value;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <inheritdoc/>
    public bool IsDirty {
        get {
            Shard[] currentShards = Atomic.Read(ref this._shards);
            for(int i = 0; i < currentShards.Length; i++) {
                if(currentShards[i].Filter.IsDirty) return true;
            }
            return false;
        }
    }

    private readonly record struct Shard(IPersistentBloomFilter Filter, UnixTimestamp Expiration);

    /// <summary>
    /// Initializes a new instance of the <see cref="RotatingBloomFilter"/> class.
    /// </summary>
    public RotatingBloomFilter(
        BloomFilterConfiguration baseConfig,
        BloomFilterContext context,
        TimeSpan windowSize,
        int shardCount) {

        Preca.ThrowIfNull(baseConfig);
        Preca.ThrowIfNull(context);
        Preca.ThrowIfLessThan(shardCount, 1, () => new ArgumentOutOfRangeException(nameof(shardCount), "Shard count must be at least 1."));

        this.Configuration = baseConfig;
        this._context = context;
        this._shardDuration = windowSize / shardCount;
        this._shards = new Shard[shardCount];

        long itemsPerShard = (long)Math.Ceiling((double)baseConfig.ExpectedItems / shardCount);
        UnixTimestamp alignedNow = AlignTimestamp(context.TimeProvider.GetUnixTimestamp(), this._shardDuration);

        // Pre-allocate sliding shards ordered from oldest to active
        for(int i = 0; i < shardCount; i++) {
            int offsetFromActive = i - (shardCount - 1);
            UnixTimestamp expiration = alignedNow + (this._shardDuration * (offsetFromActive + 1));
            this._shards[i] = CreateShard(expiration, itemsPerShard);
        }
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        EnsureActiveShard();

        this._lock.EnterReadLock();
        try {
            Shard[] currentShards = Atomic.Read(ref this._shards);
            return currentShards[^1].Filter.Add(item);
        }
        finally {
            this._lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        EnsureActiveShard();

        this._lock.EnterReadLock();
        try {
            Shard[] currentShards = Atomic.Read(ref this._shards);
            for(int i = currentShards.Length - 1; i >= 0; i--) {
                if(currentShards[i].Filter.Contains(item)) return true;
            }
            return false;
        }
        finally {
            this._lock.ExitReadLock();
        }
    }

    private void EnsureActiveShard() {
        UnixTimestamp now = this._context.TimeProvider.GetUnixTimestamp();
        Shard[] currentShards = Atomic.Read(ref this._shards);

        if(now < currentShards[^1].Expiration) return;

        this._lock.EnterWriteLock();
        try {
            currentShards = Atomic.Read(ref this._shards);
            if(now < currentShards[^1].Expiration) return;

            long elapsedMs = (long)(now - currentShards[^1].Expiration).TotalMilliseconds;
            long durationMs = (long)this._shardDuration.TotalMilliseconds;
            int shifts = durationMs > 0 ? 1 + (int)(elapsedMs / durationMs) : 1;
            shifts = Math.Min(shifts, currentShards.Length);

            var newShards = new Shard[currentShards.Length];
            long itemsPerShard = currentShards[0].Filter.Configuration.ExpectedItems;

            for(int i = 0; i < shifts; i++) {
                IPersistentBloomFilter deadFilter = currentShards[i].Filter;
                if(deadFilter is IDisposable d) d.Dispose();

                if(this._context.Storage != null) {
                    _ = this._context.Storage.DeleteAsync(deadFilter.Name, CancellationToken.None);
                }
            }

            int remaining = currentShards.Length - shifts;
            if(remaining > 0) {
                Array.Copy(currentShards, shifts, newShards, 0, remaining);
            }

            UnixTimestamp baseExpiration = remaining > 0
                ? currentShards[^1].Expiration
                : AlignTimestamp(now, this._shardDuration);

            for(int i = remaining; i < newShards.Length; i++) {
                UnixTimestamp expiration = baseExpiration + (this._shardDuration * (i - remaining + 1));
                newShards[i] = CreateShard(expiration, itemsPerShard);
            }

            Atomic.Write(ref this._shards, newShards);
        }
        finally {
            this._lock.ExitWriteLock();
        }
    }

    private Shard CreateShard(UnixTimestamp expiration, long expectedItems) {
        // Use deterministic time-slot identifier to prevent key collisions across restarts
        long durationMs = (long)this._shardDuration.TotalMilliseconds;
        long windowSlot = durationMs > 0 ? expiration.TotalMilliseconds / durationMs : expiration.TotalMilliseconds;

        FilterName shardName = FilterName.Parse($"{this.Configuration.Name.Value}_W{windowSlot}");
        BloomFilterConfiguration config = this._context.ConfigFactory.Create(
            shardName,
            expectedItems,
            this.Configuration.ErrorRate,
            this.Configuration.HashSeed + windowSlot
        );

        long totalBytes = (config.SizeInBits + 7) / 8;
        IPersistentBloomFilter filter = (totalBytes > this._context.Options.Lifecycle.ShardingThresholdBytes)
            ? new ShardedBloomFilter(config.WithShardCount((int)BitOperations.RoundUpToPowerOf2((uint)Math.Ceiling((double)totalBytes / this._context.Options.Lifecycle.ShardingThresholdBytes))), this._context)
            : new InMemoryBloomFilter(config, this._context);

        return new Shard(filter, expiration);
    }

    private static UnixTimestamp AlignTimestamp(UnixTimestamp timestamp, TimeSpan duration) {
        long ms = timestamp.TotalMilliseconds;
        long durationMs = (long)duration.TotalMilliseconds;
        if(durationMs <= 0) return timestamp;
        long mod = ms % durationMs;
        if(ms < 0 && mod != 0) mod += durationMs;
        return UnixTimestamp.FromMilliseconds(ms - mod);
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        Shard[] currentShards = Atomic.Read(ref this._shards);

        IEnumerable<Task> saveTasks = currentShards
            .Where(s => s.Filter.IsDirty)
            .Select(s => s.Filter.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);
        Shard[] currentShards = Atomic.Read(ref this._shards);

        IEnumerable<Task> reloadTasks = currentShards.Select(s => s.Filter.ReloadAsync(cancellationToken).AsTask());
        await Task.WhenAll(reloadTasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Add(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Contains(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        this._lock.EnterReadLock();
        try {
            long total = 0;
            Shard[] currentShards = Atomic.Read(ref this._shards);
            for(int i = 0; i < currentShards.Length; i++) {
                total += currentShards[i].Filter.GetPopCount();
            }
            return total;
        }
        finally {
            this._lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._lock.EnterWriteLock();
            try {
                Shard[] currentShards = Atomic.Read(ref this._shards);
                foreach(Shard shard in currentShards) {
                    if(shard.Filter is IDisposable d) d.Dispose();
                }
            }
            finally {
                this._lock.ExitWriteLock();
                this._lock.Dispose();
            }
            this._disposeState.SetDisposed();
        }
    }
}