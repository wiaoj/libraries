using System.Numerics;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Advanced;

/// <summary>
/// A time-windowed, persistent Bloom Filter that rotates underlying shards based on a Time-To-Live (TTL).
/// Automatically discards old data to maintain a clean sliding window (e.g., "Unique Users in Last 7 Days").
/// </summary>
public sealed class RotatingBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly TimeSpan _shardDuration;
    private readonly DisposeState _disposeState = new();

    private Shard[] _shards;
    private long _shardCounter;

    /// <inheritdoc/>
    public string Name => Configuration.Name;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether any of the active shards have been modified and require persistence.
    /// </summary>
    public bool IsDirty {
        get {
            var currentShards = Atomic.Read(ref _shards);
            for(int i = 0; i < currentShards.Length; i++) {
                if(currentShards[i].Filter.IsDirty) return true;
            }
            return false;
        }
    }

    // Internal structure representing a shard and its expiration timestamp.
    private readonly record struct Shard(IPersistentBloomFilter Filter, UnixTimestamp Expiration);

    /// <summary>
    /// Initializes a new instance of the <see cref="RotatingBloomFilter"/> class.
    /// </summary>
    /// <param name="baseConfig">The base configuration for individual shards.</param>
    /// <param name="context">The shared context containing logging, storage, and factory services.</param>
    /// <param name="windowSize">The total time window to cover (e.g., 7 days).</param>
    /// <param name="shardCount">The number of shards to split the window into (e.g., 7 shards for 7 days).</param>
    public RotatingBloomFilter(
        BloomFilterConfiguration baseConfig,
        BloomFilterContext context,
        TimeSpan windowSize,
        int shardCount) {

        Preca.ThrowIfNull(baseConfig);
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNegativeOrZero(shardCount);

        Configuration = baseConfig;
        _context = context;
        _shardDuration = windowSize / shardCount;
        _shards = new Shard[shardCount];

        long itemsPerShard = (long)Math.Ceiling((double)baseConfig.ExpectedItems / shardCount);

        // Time Alignment: Ensure the start point is smooth (e.g., align to midnight for daily shards).
        UnixTimestamp alignedNow = AlignTimestamp(context.TimeProvider.GetUnixTimestamp(), _shardDuration);

        // Pre-allocate shards for the initial window.
        for(int i = 0; i < shardCount; i++) {
            var expiration = alignedNow + (_shardDuration * (i + 1));
            _shards[i] = CreateShard(expiration, itemsPerShard);
        }
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);

        // Ensure the active time window is current; rotate shards if necessary.
        EnsureActiveShard();

        _lock.EnterReadLock();
        try {
            var currentShards = Atomic.Read(ref _shards);
            // Additions are always performed on the newest (active) shard.
            return currentShards[^1].Filter.Add(item);
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);

        // Ensure shards are up-to-date before searching.
        EnsureActiveShard();

        _lock.EnterReadLock();
        try {
            var currentShards = Atomic.Read(ref _shards);
            // Zero-allocation search: iterate from newest to oldest data.
            for(int i = currentShards.Length - 1; i >= 0; i--) {
                if(currentShards[i].Filter.Contains(item)) return true;
            }
            return false;
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    private void EnsureActiveShard() {
        var now = _context.TimeProvider.GetUnixTimestamp();
        var currentShards = Atomic.Read(ref _shards);

        // Fast lock-free check. If the active shard has not expired, exit.
        if(now <= currentShards[^1].Expiration) return;

        _lock.EnterWriteLock();
        try {
            // Double-check: another thread might have performed the rotation.
            currentShards = Atomic.Read(ref _shards);
            if(now <= currentShards[^1].Expiration) return;

            // Determine how many shards have expired (multiple shards may be skipped if the app was offline).
            int shifts = 1;
            while(now > currentShards[^1].Expiration + (_shardDuration * shifts) && shifts < currentShards.Length) {
                shifts++;
            }

            var newShards = new Shard[currentShards.Length];
            long itemsPerShard = currentShards[0].Filter.Configuration.ExpectedItems;

            // 1. Cleanup expired shards (release memory and persistent storage).
            for(int i = 0; i < shifts; i++) {
                var deadFilter = currentShards[i].Filter;

                // Dispose in-memory resources.
                if(deadFilter is IDisposable d) d.Dispose();

                // Permanently delete from storage provider (Fire-and-Forget).
                if(_context.Storage != null) {
                    _ = _context.Storage.DeleteAsync(deadFilter.Name, CancellationToken.None);
                }
            }

            // 2. Shift remaining active shards to the left.
            int remaining = currentShards.Length - shifts;
            if(remaining > 0) {
                Array.Copy(currentShards, shifts, newShards, 0, remaining);
            }

            // 3. Create fresh shards for the future at the end of the array.
            var baseExpiration = remaining > 0 ? currentShards[^1].Expiration : AlignTimestamp(now, _shardDuration);

            for(int i = remaining; i < newShards.Length; i++) {
                var expiration = baseExpiration + (_shardDuration * (i - remaining + 1));
                newShards[i] = CreateShard(expiration, itemsPerShard);
            }

            // Atomically swap the shard array.
            Atomic.Write(ref _shards, newShards);
        }
        finally {
            _lock.ExitWriteLock();
        }
    }

    private Shard CreateShard(UnixTimestamp expiration, long expectedItems) {
        long nextId = Interlocked.Increment(ref _shardCounter);

        var config = _context.ConfigFactory.Create(
            FilterName.Parse($"{Configuration.Name.Value}_W{nextId}"),
            expectedItems,
            Configuration.ErrorRate,
            Configuration.HashSeed + (uint)nextId
        );

        // Intelligent Layer Factory: selects InMemory or Sharded based on size.
        long totalBytes = (config.SizeInBits + 7) / 8;
        IPersistentBloomFilter filter = (totalBytes > _context.Options.Lifecycle.ShardingThresholdBytes)
            ? new ShardedBloomFilter(config.WithShardCount((int)BitOperations.RoundUpToPowerOf2((uint)Math.Ceiling((double)totalBytes / _context.Options.Lifecycle.ShardingThresholdBytes))), _context)
            : new InMemoryBloomFilter(config, _context);

        return new Shard(filter, expiration);
    }

    /// <summary>
    /// Rounds down the timestamp to the nearest duration boundary.
    /// E.g., if duration is 1 day, it aligns to 00:00:00 UTC.
    /// </summary>
    private static UnixTimestamp AlignTimestamp(UnixTimestamp timestamp, TimeSpan duration) {
        long ms = timestamp.TotalMilliseconds;
        long durationMs = (long)duration.TotalMilliseconds;
        long mod = ms % durationMs;
        if(ms < 0 && mod != 0) mod += durationMs;
        return UnixTimestamp.FromMilliseconds(ms - mod);
    }

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);
        var currentShards = Atomic.Read(ref _shards);

        // Persist only the active shards that have pending modifications.
        var saveTasks = currentShards
            .Where(s => s.Filter.IsDirty)
            .Select(s => s.Filter.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks);
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);
        var currentShards = Atomic.Read(ref _shards);

        var reloadTasks = currentShards.Select(s => s.Filter.ReloadAsync(cancellationToken).AsTask());
        await Task.WhenAll(reloadTasks);
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
        _lock.EnterReadLock();
        try {
            long total = 0;
            var currentShards = Atomic.Read(ref _shards);
            for(int i = 0; i < currentShards.Length; i++) total += currentShards[i].Filter.GetPopCount();
            return total;
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Releases all resources used by the Rotating Bloom Filter and its underlying shards.
    /// </summary>
    public void Dispose() {
        if(_disposeState.TryBeginDispose()) {
            _lock.EnterWriteLock();
            try {
                var currentShards = Atomic.Read(ref _shards);
                foreach(var shard in currentShards) {
                    if(shard.Filter is IDisposable d) d.Dispose();
                }
            }
            finally {
                _lock.ExitWriteLock();
                _lock.Dispose();
            }
            _disposeState.SetDisposed();
        }
    }
}