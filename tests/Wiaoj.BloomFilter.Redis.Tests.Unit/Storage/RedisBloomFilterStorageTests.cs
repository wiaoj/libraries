using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using System.IO.Compression;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.BloomFilter.Redis.Storage;

namespace Wiaoj.BloomFilter.Redis.Tests.Unit.Storage;

public class RedisBloomFilterStorageTests {
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;

    private static BloomFilterConfiguration CreateConfig(string name = "test") =>
        new(FilterName.Parse(name), 1000, 0.01, 10000, 7, 0);

    public RedisBloomFilterStorageTests() {
        this._multiplexer = Substitute.For<IConnectionMultiplexer>();
        this._database = Substitute.For<IDatabase>();
        this._multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(this._database);
    }

    private RedisBloomFilterStorage CreateStorage(
        string keyPrefix = "bloom:snapshot:",
        TimeSpan? ttl = null,
        bool enableCompression = false,
        bool ignoreErrors = false,
        int? database = null) {
        RedisBloomFilterStorageOptions options = new() {
            KeyPrefix = keyPrefix,
            Ttl = ttl,
            EnableCompression = enableCompression,
            IgnoreErrors = ignoreErrors,
            Database = database
        };

        return new RedisBloomFilterStorage(
            this._multiplexer,
            Microsoft.Extensions.Options.Options.Create(options),
            NullLogger<RedisBloomFilterStorage>.Instance);
    }

    [Fact]
    public void Constructor_Should_ThrowOnNullArguments() {
        IOptions<RedisBloomFilterStorageOptions> options = Microsoft.Extensions.Options.Options.Create(new RedisBloomFilterStorageOptions());

        Assert.ThrowsAny<ArgumentNullException>(() => new RedisBloomFilterStorage(null!, options, NullLogger<RedisBloomFilterStorage>.Instance));
        Assert.ThrowsAny<ArgumentNullException>(() => new RedisBloomFilterStorage(this._multiplexer, null!, NullLogger<RedisBloomFilterStorage>.Instance));
        Assert.ThrowsAny<ArgumentNullException>(() => new RedisBloomFilterStorage(this._multiplexer, options, null!));
    }

    [Fact]
    public async Task SaveAsync_Should_ThrowOnInvalidArguments() {
        RedisBloomFilterStorage storage = CreateStorage();
        FilterName validName = FilterName.Parse("users");
        BloomFilterConfiguration validConfig = CreateConfig();
        using MemoryStream stream = new([1, 2, 3]);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.SaveAsync(default, validConfig, stream));
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => storage.SaveAsync(validName, null!, stream));
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => storage.SaveAsync(validName, validConfig, null!));
    }

    [Fact]
    public async Task LoadStreamAsync_And_DeleteAsync_Should_ThrowOnDefaultFilterName() {
        RedisBloomFilterStorage storage = CreateStorage();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.LoadStreamAsync(default).AsTask());
        await Assert.ThrowsAnyAsync<ArgumentException>(() => storage.DeleteAsync(default));
    }

    [Fact]
    public async Task Should_SaveAndLoadStream_Successfully_WithoutCompression() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(enableCompression: false);
        FilterName filterName = FilterName.Parse("users");
        BloomFilterConfiguration config = CreateConfig();
        byte[] originalData = Encoding.UTF8.GetBytes("bloom filter raw bit contents");
        using MemoryStream saveStream = new(originalData);

        byte[]? capturedBytes = null;
        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Do<RedisValue>(v => capturedBytes = (byte[])v!),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(_ => (RedisValue)capturedBytes!);

        // Act
        bool saved = await storage.SaveAsync(filterName, config, saveStream);
        var loaded = await storage.LoadStreamAsync(filterName);

        // Assert
        Assert.True(saved);
        Assert.NotNull(loaded);
        using MemoryStream loadedStream = new();
        await loaded.Value.DataStream.CopyToAsync(loadedStream);
        Assert.Equal(originalData, loadedStream.ToArray());
    }

    [Fact]
    public async Task Should_SaveAndLoadStream_Successfully_WithCompression() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(enableCompression: true);
        FilterName filterName = FilterName.Parse("compressed_filter");
        BloomFilterConfiguration config = CreateConfig();
        byte[] originalData = Encoding.UTF8.GetBytes("highly compressible repetitive content repetitive content");
        using MemoryStream saveStream = new(originalData);

        byte[]? capturedBytes = null;
        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Do<RedisValue>(v => capturedBytes = (byte[])v!),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(_ => (RedisValue)capturedBytes!);

        // Act
        bool saved = await storage.SaveAsync(filterName, config, saveStream);
        var loaded = await storage.LoadStreamAsync(filterName);

        // Assert
        Assert.True(saved);
        Assert.NotNull(loaded);
        using MemoryStream loadedStream = new();
        await loaded.Value.DataStream.CopyToAsync(loadedStream);
        Assert.Equal(originalData, loadedStream.ToArray());
    }

    [Fact]
    public async Task Should_SaveAndLoad_EmptyStream() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(enableCompression: false);
        FilterName filterName = FilterName.Parse("empty_filter");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new();

        byte[]? capturedBytes = null;
        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Do<RedisValue>(v => capturedBytes = (byte[])v!),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(_ => (RedisValue)capturedBytes!);

        // Act
        bool saved = await storage.SaveAsync(filterName, config, saveStream);
        var loaded = await storage.LoadStreamAsync(filterName);

        // Assert
        Assert.True(saved);
        Assert.NotNull(loaded);
        using MemoryStream loadedStream = new();
        await loaded.Value.DataStream.CopyToAsync(loadedStream);
        Assert.Empty(loadedStream.ToArray());
    }

    [Fact]
    public async Task Should_ApplyKeyPrefixAndTtl_OnSave() {
        // Arrange
        TimeSpan ttl = TimeSpan.FromHours(2);
        string prefix = "custom:prefix:";
        RedisBloomFilterStorage storage = CreateStorage(keyPrefix: prefix, ttl: ttl);
        FilterName filterName = FilterName.Parse("orders");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));

        RedisKey capturedKey = default;
        TimeSpan? capturedTtl = null;
        this._database.StringSetAsync(
            Arg.Do<RedisKey>(k => capturedKey = k),
            Arg.Any<RedisValue>(),
            Arg.Do<TimeSpan?>(t => capturedTtl = t),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        // Act
        await storage.SaveAsync(filterName, config, saveStream);

        // Assert
        Assert.Equal("custom:prefix:orders", (string)capturedKey!);
        Assert.Equal(ttl, capturedTtl);
    }

    [Fact]
    public async Task Should_RouteToCustomDatabaseIndex_WhenConfigured() {
        // Arrange
        int targetDb = 5;
        RedisBloomFilterStorage storage = CreateStorage(database: targetDb);
        FilterName filterName = FilterName.Parse("custom_db");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));

        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        // Act
        await storage.SaveAsync(filterName, config, saveStream);

        // Assert
        this._multiplexer.Received(1).GetDatabase(targetDb, Arg.Any<object>());
    }

    [Fact]
    public async Task Should_DeleteAsync_CallKeyDelete() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(keyPrefix: "app:bloom:");
        FilterName filterName = FilterName.Parse("products");

        RedisKey capturedKey = default;
        this._database.KeyDeleteAsync(
            Arg.Do<RedisKey>(k => capturedKey = k),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        // Act
        await storage.DeleteAsync(filterName);

        // Assert
        Assert.Equal("app:bloom:products", (string)capturedKey!);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenKeyDoesNotExist() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage();
        FilterName filterName = FilterName.Parse("nonexistent");
        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(RedisValue.Null));

        // Act
        var result = await storage.LoadStreamAsync(filterName);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task Should_IgnoreErrors_WhenConfigured() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(ignoreErrors: true);
        FilterName filterName = FilterName.Parse("faulty");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));

        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns<Task<bool>>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis offline"));

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<RedisValue>>(_ => throw new RedisTimeoutException("Timeout", CommandStatus.Unknown));

        this._database.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<bool>>(_ => throw new RedisServerException("Redis memory full"));

        // Act & Assert
        bool saved = await storage.SaveAsync(filterName, config, saveStream);
        Assert.False(saved);

        var loaded = await storage.LoadStreamAsync(filterName);
        Assert.Null(loaded);

        await storage.DeleteAsync(filterName); // Should not throw
    }

    [Fact]
    public async Task Should_ThrowException_WhenIgnoreErrorsIsFalse() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(ignoreErrors: false);
        FilterName filterName = FilterName.Parse("faulty");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));

        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns<Task<bool>>(_ => throw new RedisTimeoutException("Timeout", CommandStatus.Unknown));

        // Act & Assert
        await Assert.ThrowsAsync<RedisTimeoutException>(() => storage.SaveAsync(filterName, config, saveStream));
    }

    [Fact]
    public async Task Should_ThrowIfCancellationRequested() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage();
        FilterName filterName = FilterName.Parse("cancelled");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));
        using CancellationTokenSource cts = new();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await storage.SaveAsync(filterName, config, saveStream, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await storage.LoadStreamAsync(filterName, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await storage.DeleteAsync(filterName, cts.Token));
    }

    [Fact]
    public async Task FilterName_WithSpecialCharacters_Should_ProduceCorrectRedisKey() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(keyPrefix: "app:tenants:");
        FilterName filterName = FilterName.Parse("tenant-42_orders.v1");
        BloomFilterConfiguration config = CreateConfig();
        using MemoryStream saveStream = new(Encoding.UTF8.GetBytes("test"));

        RedisKey capturedKey = default;
        this._database.StringSetAsync(
            Arg.Do<RedisKey>(k => capturedKey = k),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        // Act
        await storage.SaveAsync(filterName, config, saveStream);

        // Assert
        Assert.Equal("app:tenants:tenant-42_orders.v1", (string)capturedKey!);
    }

    [Fact]
    public async Task DeleteAsync_WhenIgnoreErrorsIsTrue_Should_NotThrowOnRedisException() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(ignoreErrors: true);
        FilterName filterName = FilterName.Parse("error_on_delete");

        this._database.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns<Task<bool>>(_ => throw new RedisServerException("CLUSTERDOWN Hash slot not served"));

        // Act & Assert - Should not throw
        await storage.DeleteAsync(filterName);
    }

    [Fact]
    public async Task ParameterlessOverloads_WithoutCancellationToken_Should_Succeed() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage();
        FilterName filterName = FilterName.Parse("parameterless");
        BloomFilterConfiguration config = CreateConfig();
        byte[] payload = [1, 2, 3, 4];
        using MemoryStream saveStream = new(payload);

        this._database.StringSetAsync(
            Arg.Any<RedisKey>(),
            Arg.Any<RedisValue>(),
            Arg.Any<TimeSpan?>(),
            Arg.Any<bool>(),
            Arg.Any<When>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(true));

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult((RedisValue)payload));

        this._database.KeyDeleteAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act & Assert
        bool saved = await storage.SaveAsync(filterName, config, saveStream);
        Assert.True(saved);

        var loaded = await storage.LoadStreamAsync(filterName);
        Assert.NotNull(loaded);

        await storage.DeleteAsync(filterName);
    }

    [Fact]
    public async Task LoadStreamAsync_WithCorruptedCompressedData_Should_ThrowInvalidDataException_WhenRead() {
        // Arrange
        RedisBloomFilterStorage storage = CreateStorage(enableCompression: true);
        FilterName filterName = FilterName.Parse("corrupted_gzip");
        byte[] invalidGzipBytes = [0xFF, 0xFE, 0xFD, 0xFC]; // Not valid GZip header

        this._database.StringGetAsync(Arg.Any<RedisKey>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult((RedisValue)invalidGzipBytes));

        // Act
        var loaded = await storage.LoadStreamAsync(filterName);
        Assert.NotNull(loaded);

        // Assert - Attempting to read/decompress invalid GZip data throws InvalidDataException
        using MemoryStream dest = new();
        await Assert.ThrowsAsync<InvalidDataException>(async () => await loaded.Value.DataStream.CopyToAsync(dest));
    }
}
