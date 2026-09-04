using Microsoft.Extensions.Logging;
using Wiaoj.BloomFilter.Diagnostics;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Engine;
/// <summary>
/// A sliding-window Bloom Filter that rotates underlying time shards based on a configured Time-To-Live (TTL).
/// Drops expired time windows while maintaining queryability across active sliding windows.
/// </summary>
internal sealed class RotatingBloomFilter : BloomFilterBase {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly TimeSpan _shardDuration;

    private Shard[] _shards;

    /// <inheritdoc/>
    public override FilterName Name => this.Configuration.Name;

    /// <inheritdoc/>
    public override BloomFilterConfiguration Configuration { get; }

    /// <inheritdoc/>
    public override bool IsDirty {
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
    public override bool Add(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();
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
    public override bool Contains(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();
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

        // Try to acquire write lock without queuing; if another thread is already rotating,
        // do not queue up behind it (prevents lock convoy on expiry window transition).
        if(!this._lock.TryEnterWriteLock(0)) {
            return;
        }

        try {
            currentShards = Atomic.Read(ref this._shards);
            if(now < currentShards[^1].Expiration) return;

            long elapsedMs = (long)(now - currentShards[^1].Expiration).TotalMilliseconds;
            long durationMs = (long)this._shardDuration.TotalMilliseconds;
            int shifts = durationMs > 0 ? 1 + (int)(elapsedMs / durationMs) : 1;
            shifts = Math.Min(shifts, currentShards.Length);

            Shard[] newShards = new Shard[currentShards.Length];
            long itemsPerShard = currentShards[0].Filter.Configuration.ExpectedItems;

            for(int i = 0; i < shifts; i++) {
                IPersistentBloomFilter deadFilter = currentShards[i].Filter;
                if(deadFilter is IDisposable d) d.Dispose();

                if(this._context.Storage != null) {
                    FilterName deadFilterName = deadFilter.Name;
                    IBloomFilterStorage storage = this._context.Storage;
                    ILogger logger = this._context.Logger;
                    _ = Task.Run(async () => {
                        try {
                            await storage.DeleteAsync(deadFilterName, CancellationToken.None).ConfigureAwait(false);
                        }
                        catch(Exception ex) {
                            logger.LogStorageDeleteFailed(ex, deadFilterName);
                        }
                    });
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

        IPersistentBloomFilter filter = this._context.CreateLeafFilter(config);

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
    public override async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        Shard[] currentShards = Atomic.Read(ref this._shards);

        IEnumerable<Task> saveTasks = currentShards
            .Where(s => s.Filter.IsDirty)
            .Select(s => s.Filter.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();

        Shard[] currentShards = Atomic.Read(ref this._shards);

        IEnumerable<Task> reloadTasks = currentShards.Select(s => s.Filter.ReloadAsync(cancellationToken).AsTask());
        await Task.WhenAll(reloadTasks).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override long GetPopCount() {
        ThrowIfDisposed();
        EnsureActiveShard();

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
    protected override void DisposeCore() {
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
    }
}