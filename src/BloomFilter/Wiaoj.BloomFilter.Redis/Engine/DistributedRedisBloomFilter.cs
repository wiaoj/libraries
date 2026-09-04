using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Buffers;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Redis.Engine;

/// <summary>
/// Distributed remote Bloom Filter that delegates bit-level storage to Redis.
/// Computes hash positions client-side using Kirsch-Mitzenmacher double hashing and pipelines all
/// bit operations via <see cref="IBatch"/> in a single round-trip.
/// </summary>
public class DistributedRedisBloomFilter : IBloomFilter, IAsyncBloomFilter {
    private readonly IConnectionMultiplexer _redis;
    private readonly BloomFilterConfiguration _configuration;
    private readonly DistributedBloomFilterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistributedRedisBloomFilter"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="configuration">The immutable filter configuration parameters.</param>
    /// <param name="options">The distributed filter options.</param>
    public DistributedRedisBloomFilter(
        IConnectionMultiplexer redis,
        BloomFilterConfiguration configuration,
        IOptions<DistributedBloomFilterOptions> options) {
        Preca.ThrowIfNull(redis);
        Preca.ThrowIfNull(configuration);
        Preca.ThrowIfNull(options);

        this._redis = redis;
        this._configuration = configuration;
        this._options = options.Value;
    }

    /// <inheritdoc/>
    public FilterName Name => this._configuration.Name;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration => this._configuration;

    private IDatabase Db => this._redis.GetDatabase(this._options.Database ?? -1);

    private RedisKey RedisKey => $"{this._options.KeyPrefix}{this.Name.Value}";

    /// <inheritdoc/>
    public async ValueTask<bool> AddAsync(ReadOnlyMemory<byte> item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        BloomHasher.ComputeBaseHashes(item.Span, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        IBatch batch = this.Db.CreateBatch();
        int k = this.Configuration.HashFunctionCount;
        Task<bool>[] bitTasks = new Task<bool>[k];

        for (int i = 0; i < k; i++) {
            long bitPos = BloomHasher.GetBitPosition(h1, h2, i, this.Configuration.SizeInBits);
            bitTasks[i] = batch.StringSetBitAsync(this.RedisKey, bitPos, true);
        }

        batch.Execute();
        if (cancellationToken.CanBeCanceled) {
            await Task.WhenAll(bitTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else {
            await Task.WhenAll(bitTasks).ConfigureAwait(false);
        }

        bool anyBitChanged = false;
        for (int i = 0; i < k; i++) {
            if (!bitTasks[i].Result) {
                anyBitChanged = true;
            }
        }

        return anyBitChanged;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ContainsAsync(ReadOnlyMemory<byte> item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();

        BloomHasher.ComputeBaseHashes(item.Span, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        IBatch batch = this.Db.CreateBatch();
        int k = this.Configuration.HashFunctionCount;
        Task<bool>[] bitTasks = new Task<bool>[k];

        for (int i = 0; i < k; i++) {
            long bitPos = BloomHasher.GetBitPosition(h1, h2, i, this.Configuration.SizeInBits);
            bitTasks[i] = batch.StringGetBitAsync(this.RedisKey, bitPos);
        }

        batch.Execute();
        if (cancellationToken.CanBeCanceled) {
            await Task.WhenAll(bitTasks).WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        else {
            await Task.WhenAll(bitTasks).ConfigureAwait(false);
        }

        for (int i = 0; i < k; i++) {
            if (!bitTasks[i].Result) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> AddAsync(string item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNull(item, nameof(item));

        byte[] bytes = Encoding.UTF8.GetBytes(item);
        return await AddAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> ContainsAsync(string item, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Preca.ThrowIfNull(item, nameof(item));

        byte[] bytes = Encoding.UTF8.GetBytes(item);
        return await ContainsAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<long> GetPopCountAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        Task<long> countTask = this.Db.StringBitCountAsync(this.RedisKey, 0, -1, CommandFlags.None);
        if (cancellationToken.CanBeCanceled) {
            return await countTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return await countTask.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        IBatch batch = this.Db.CreateBatch();
        int k = this.Configuration.HashFunctionCount;
        Task<bool>[] bitTasks = new Task<bool>[k];

        for (int i = 0; i < k; i++) {
            long bitPos = BloomHasher.GetBitPosition(h1, h2, i, this.Configuration.SizeInBits);
            bitTasks[i] = batch.StringSetBitAsync(this.RedisKey, bitPos, true);
        }

        batch.Execute();
        Task.WaitAll(bitTasks);

        bool anyBitChanged = false;
        for (int i = 0; i < k; i++) {
            if (!bitTasks[i].Result) {
                anyBitChanged = true;
            }
        }

        return anyBitChanged;
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        BloomHasher.ComputeBaseHashes(item, this.Configuration.HashSeed, out ulong h1, out ulong h2);
        IBatch batch = this.Db.CreateBatch();
        int k = this.Configuration.HashFunctionCount;
        Task<bool>[] bitTasks = new Task<bool>[k];

        for (int i = 0; i < k; i++) {
            long bitPos = BloomHasher.GetBitPosition(h1, h2, i, this.Configuration.SizeInBits);
            bitTasks[i] = batch.StringGetBitAsync(this.RedisKey, bitPos);
        }

        batch.Execute();
        Task.WaitAll(bitTasks);

        for (int i = 0; i < k; i++) {
            if (!bitTasks[i].Result) {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
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
        if (item.IsEmpty) {
            return Contains(ReadOnlySpan<byte>.Empty);
        }

        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        if (maxBytes <= 256) {
            Span<byte> stackBuffer = stackalloc byte[maxBytes];
            int bytesWritten = Encoding.UTF8.GetBytes(item, stackBuffer);
            return Contains(stackBuffer[..bytesWritten]);
        }

        byte[] array = ArrayPool<byte>.Shared.Rent(maxBytes);
        try {
            int bytesWritten = Encoding.UTF8.GetBytes(item, array);
            return Contains(array.AsSpan(0, bytesWritten));
        }
        finally {
            ArrayPool<byte>.Shared.Return(array);
        }
    }

    /// <inheritdoc/>
    public long GetPopCount() {
        return this.Db.StringBitCount(this.RedisKey, 0, -1, CommandFlags.None);
    }
}
