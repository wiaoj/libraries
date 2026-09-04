using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Buffers;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Messaging;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Redis.Engine;

/// <summary>
/// Hybrid Bloom Filter combining local L1 SIMD memory queries (10–50ns reads)
/// with real-time Redis Pub/Sub delta replication (32-byte wire messages) to synchronize peer nodes.
/// </summary>
public class SynchronizedRedisBloomFilter : IBloomFilter, IAsyncBloomFilter, IPersistentBloomFilter, IDisposable {
    private readonly IConnectionMultiplexer _redis;
    private readonly InMemoryBloomFilter _innerFilter;
    private readonly SynchronizedBloomFilterOptions _options;
    private readonly ILogger<SynchronizedRedisBloomFilter> _logger;
    private readonly ISubscriber _subscriber;
    private readonly Action<RedisChannel, RedisValue> _messageHandler;
    private bool _disposed;

    /// <summary>
    /// Gets the unique node identifier used to avoid self-echo message loops.
    /// </summary>
    public Guid NodeId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedRedisBloomFilter"/> class with default logging.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="innerFilter">The local in-memory Bloom filter instance.</param>
    /// <param name="options">The synchronized filter configuration options.</param>
    internal SynchronizedRedisBloomFilter(
        IConnectionMultiplexer redis,
        InMemoryBloomFilter innerFilter,
        IOptions<SynchronizedBloomFilterOptions> options)
        : this(redis, innerFilter, options, NullLogger<SynchronizedRedisBloomFilter>.Instance) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SynchronizedRedisBloomFilter"/> class with custom logging.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="innerFilter">The local in-memory Bloom filter instance.</param>
    /// <param name="options">The synchronized filter configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    internal SynchronizedRedisBloomFilter(
        IConnectionMultiplexer redis,
        InMemoryBloomFilter innerFilter,
        IOptions<SynchronizedBloomFilterOptions> options,
        ILogger<SynchronizedRedisBloomFilter> logger) {
        Preca.ThrowIfNull(redis);
        Preca.ThrowIfNull(innerFilter);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._redis = redis;
        this._innerFilter = innerFilter;
        this._options = options.Value;
        this._logger = logger;
        this.NodeId = this._options.NodeId ?? Guid.NewGuid();

        this._subscriber = this._redis.GetSubscriber();
        this._messageHandler = this.OnMessageReceived;
        this._subscriber.Subscribe(this.Channel, this._messageHandler);
    }

    /// <inheritdoc/>
    public FilterName Name => this._innerFilter.Name;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration => this._innerFilter.Configuration;

    /// <inheritdoc/>
    public bool IsDirty => this._options.EnableSnapshotPersistence && this._innerFilter.IsDirty;

    private RedisChannel Channel => RedisChannel.Literal($"{this._options.SyncChannelPrefix}{this.Name.Value}");

    private void OnMessageReceived(RedisChannel channel, RedisValue value) {
        if (this._disposed || value.IsNullOrEmpty) {
            return;
        }

        try {
            ReadOnlyMemory<byte> memory = value;
            if (BloomFilterSyncMessage.TryParse(memory.Span, out BloomFilterSyncMessage syncMsg)) {
                if (syncMsg.OriginNodeId != this.NodeId) {
                    this._innerFilter.AddWithHashes(syncMsg.Hash1, syncMsg.Hash2);
                }
            }
        }
        catch (Exception ex) {
            this._logger.LogWarning(ex, "Failed to process incoming Bloom Filter sync message on channel '{Channel}'.", channel);
        }
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();

        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        bool added = this._innerFilter.AddWithHashes(h1, h2);

        if (added) {
            try {
                BloomFilterSyncMessage message = new(this.NodeId, h1, h2);
                byte[] payload = message.ToByteArray();
                this._subscriber.PublishAsync(this.Channel, payload, CommandFlags.FireAndForget);
            }
            catch (Exception ex) {
                this._logger.LogWarning(ex, "Failed to publish sync delta for Bloom Filter '{FilterName}' to channel '{Channel}'.", this.Name.Value, this.Channel);
            }
        }

        return added;
    }

    /// <inheritdoc/>
    public ValueTask<bool> AddAsync(ReadOnlyMemory<byte> item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Add(item.Span));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        ThrowIfDisposed();
        return this._innerFilter.Contains(item);
    }

    /// <inheritdoc/>
    public ValueTask<bool> ContainsAsync(ReadOnlyMemory<byte> item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Contains(item.Span));
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        ThrowIfDisposed();

        if (item.IsEmpty) {
            return Add(ReadOnlySpan<byte>.Empty);
        }

        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        if (maxBytes <= 256) {
            Span<byte> stackBuffer = stackalloc byte[maxBytes];
            int bytesWritten = Encoding.UTF8.GetBytes(item, stackBuffer);
            return Add(stackBuffer[..bytesWritten]);
        }

        byte[] array = ArrayPool<byte>.Shared.Rent(maxBytes);
        try {
            int bytesWritten = Encoding.UTF8.GetBytes(item, array);
            return Add(array.AsSpan(0, bytesWritten));
        }
        finally {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        ThrowIfDisposed();
        return this._innerFilter.Contains(item);
    }

    /// <inheritdoc/>
    public ValueTask<bool> AddAsync(string item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNull(item, nameof(item));

        return ValueTask.FromResult(Add(item.AsSpan()));
    }

    /// <inheritdoc/>
    public ValueTask<bool> ContainsAsync(string item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNull(item, nameof(item));

        return ValueTask.FromResult(Contains(item.AsSpan()));
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        ThrowIfDisposed();
        return this._innerFilter.GetPopCount();
    }

    /// <inheritdoc/>
    public ValueTask<long> GetPopCountAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(GetPopCount());
    }

    /// <inheritdoc/>
    public ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (!this._options.EnableSnapshotPersistence) {
            return ValueTask.CompletedTask;
        }

        return this._innerFilter.SaveAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        ThrowIfDisposed();
        if (!this._options.EnableSnapshotPersistence) {
            return ValueTask.CompletedTask;
        }

        return this._innerFilter.ReloadAsync(cancellationToken);
    }

    private void ThrowIfDisposed() {
        ObjectDisposedException.ThrowIf(this._disposed, this);
    }

    /// <inheritdoc/>
    public void Dispose() {
        if (this._disposed) {
            return;
        }

        this._disposed = true;
        try {
            this._subscriber.Unsubscribe(this.Channel, this._messageHandler);
        }
        catch {
            // Best-effort cleanup on shutdown
        }

        this._innerFilter.Dispose();
        GC.SuppressFinalize(this);
    }
}
