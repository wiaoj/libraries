using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.IO.Compression;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.Preconditions;

namespace Wiaoj.BloomFilter.Redis.Storage;

/// <summary>
/// Redis-backed persistence storage provider for Bloom Filter snapshots.
/// Stores serialized filter bit arrays as Redis binary strings with optional GZip compression and TTL.
/// </summary>
public sealed class RedisBloomFilterStorage : IBloomFilterStorage {
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisBloomFilterStorageOptions _options;
    private readonly ILogger<RedisBloomFilterStorage> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisBloomFilterStorage"/> class with default logging.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="options">The storage options.</param>
    public RedisBloomFilterStorage(
        IConnectionMultiplexer redis,
        IOptions<RedisBloomFilterStorageOptions> options)
        : this(redis, options, NullLogger<RedisBloomFilterStorage>.Instance) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisBloomFilterStorage"/> class with custom logging.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="options">The storage options.</param>
    /// <param name="logger">The logger instance.</param>
    public RedisBloomFilterStorage(
        IConnectionMultiplexer redis,
        IOptions<RedisBloomFilterStorageOptions> options,
        ILogger<RedisBloomFilterStorage> logger) {
        Preca.ThrowIfNull(redis);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._redis = redis;
        this._options = options.Value;
        this._logger = logger;
    }

    private IDatabase Db => this._redis.GetDatabase(this._options.Database ?? -1);

    private RedisKey GetKey(FilterName filterName) => $"{this._options.KeyPrefix}{filterName.Value}";

    /// <inheritdoc/>
    public async Task<bool> SaveAsync(
        FilterName filterName,
        BloomFilterConfiguration config,
        Stream source,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfDefault(filterName);
        Preca.ThrowIfNull(config, nameof(config));
        Preca.ThrowIfNull(source, nameof(source));
        cancellationToken.ThrowIfCancellationRequested();

        try {
            using MemoryStream ms = new();
            if (this._options.EnableCompression) {
                await using (GZipStream gzip = new(ms, CompressionLevel.Fastest, leaveOpen: true)) {
                    await source.CopyToAsync(gzip, cancellationToken).ConfigureAwait(false);
                    await gzip.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            else {
                await source.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            byte[] payload = ms.ToArray();
            bool success = await this.Db.StringSetAsync(GetKey(filterName), payload, expiry: this._options.Ttl, keepTtl: false).ConfigureAwait(false);
            return success;
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (this._options.IgnoreErrors) {
            this._logger.LogWarning(ex, "Failed to save Bloom Filter snapshot '{FilterName}' to Redis.", filterName.Value);
            return false;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(BloomFilterConfiguration? Config, Stream DataStream)?> LoadStreamAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfDefault(filterName);
        cancellationToken.ThrowIfCancellationRequested();

        try {
            RedisValue value = await this.Db.StringGetAsync(GetKey(filterName)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (value.IsNull) {
                return null;
            }

            byte[] bytes = (byte[])value!;
            Stream stream = new MemoryStream(bytes);

            if (this._options.EnableCompression) {
                stream = new GZipStream(stream, CompressionMode.Decompress);
            }

            return (null, stream);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (this._options.IgnoreErrors) {
            this._logger.LogWarning(ex, "Failed to load Bloom Filter snapshot '{FilterName}' from Redis.", filterName.Value);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        FilterName filterName,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfDefault(filterName);
        cancellationToken.ThrowIfCancellationRequested();

        try {
            RedisKey mainKey = GetKey(filterName);
            await this.Db.KeyDeleteAsync(mainKey).ConfigureAwait(false);

            // Safely delete any sharded snapshot keys matching pattern {KeyPrefix}{FilterName}_s*
            RedisValue shardPattern = (RedisValue)$"{mainKey}_s*";
            const string DeleteShardsScript = """
                local keys = redis.call('KEYS', ARGV[1])
                if #keys > 0 then
                    return redis.call('DEL', unpack(keys))
                else
                    return 0
                end
                """;
            await this.Db.ScriptEvaluateAsync(DeleteShardsScript, values: [shardPattern]).ConfigureAwait(false);
        }
        catch (OperationCanceledException) {
            throw;
        }
        catch (Exception ex) when (this._options.IgnoreErrors) {
            this._logger.LogWarning(ex, "Failed to delete Bloom Filter snapshot '{FilterName}' from Redis.", filterName.Value);
        }
    }
}
