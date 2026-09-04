using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Engine;
using Wiaoj.BloomFilter.Redis.Options;

namespace Wiaoj.BloomFilter.Redis.Tests.Unit.Engine;

public class DistributedRedisBloomFilterTests {
    private sealed record TestTag;

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;
    private readonly IBatch _batch;
    private readonly BloomFilterConfiguration _config;

    public DistributedRedisBloomFilterTests() {
        this._multiplexer = Substitute.For<IConnectionMultiplexer>();
        this._database = Substitute.For<IDatabase>();
        this._batch = Substitute.For<IBatch>();

        this._multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(this._database);
        this._database.CreateBatch(Arg.Any<object>()).Returns(this._batch);

        this._config = new BloomFilterConfiguration(
            FilterName.Parse("distributed-filter"),
            expectedItems: 1000,
            errorRate: 0.01,
            sizeInBits: 10000,
            hashFunctionCount: 5,
            hashSeed: 42);
    }

    private DistributedRedisBloomFilter CreateFilter(string keyPrefix = "bloom:live:", int? database = null) {
        DistributedBloomFilterOptions options = new() {
            KeyPrefix = keyPrefix,
            Database = database
        };

        return new DistributedRedisBloomFilter(
            this._multiplexer,
            this._config,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public void Constructor_Should_ThrowOnNullArguments() {
        IOptions<DistributedBloomFilterOptions> options = Microsoft.Extensions.Options.Options.Create(new DistributedBloomFilterOptions());

        Assert.ThrowsAny<ArgumentNullException>(() => new DistributedRedisBloomFilter(null!, this._config, options));
        Assert.ThrowsAny<ArgumentNullException>(() => new DistributedRedisBloomFilter(this._multiplexer, null!, options));
        Assert.ThrowsAny<ArgumentNullException>(() => new DistributedRedisBloomFilter(this._multiplexer, this._config, null!));
    }

    [Fact]
    public async Task AddAsync_Should_BatchSetBits_And_ReturnTrue_WhenNewBitSet() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("new-element");

        // StringSetBit returns the PREVIOUS bit value: false means it was 0, so bit changed to 1
        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));

        // Act
        bool changed = await filter.AddAsync(item);

        // Assert
        Assert.True(changed);
        await this._batch.Received(this._config.HashFunctionCount).StringSetBitAsync(
            (RedisKey)"bloom:live:distributed-filter",
            Arg.Any<long>(),
            true,
            Arg.Any<CommandFlags>());
        this._batch.Received(1).Execute();
    }

    [Fact]
    public async Task AddAsync_Should_ReturnFalse_WhenAllBitsWereAlreadySet() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("existing-element");

        // StringSetBit returns true: all bits were already 1
        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act
        bool changed = await filter.AddAsync(item);

        // Assert
        Assert.False(changed);
        this._batch.Received(1).Execute();
    }

    [Fact]
    public async Task ContainsAsync_Should_BatchGetBits_And_ReturnTrue_WhenAllBitsAreSet() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("present-element");

        this._batch.StringGetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act
        bool contains = await filter.ContainsAsync(item);

        // Assert
        Assert.True(contains);
        await this._batch.Received(this._config.HashFunctionCount).StringGetBitAsync(
            (RedisKey)"bloom:live:distributed-filter",
            Arg.Any<long>(),
            Arg.Any<CommandFlags>());
        this._batch.Received(1).Execute();
    }

    [Fact]
    public async Task ContainsAsync_Should_ReturnFalse_WhenAnyBitIsZero() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("absent-element");

        int callCount = 0;
        this._batch.StringGetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(_ => {
                callCount++;
                return Task.FromResult(callCount != 3); // 3rd bit is false
            });

        // Act
        bool contains = await filter.ContainsAsync(item);

        // Assert
        Assert.False(contains);
        this._batch.Received(1).Execute();
    }

    [Fact]
    public void Sync_AddAndContains_Should_ExecuteSuccessfully() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("sync-element");

        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        this._batch.StringGetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act
        bool added = filter.Add(item);
        bool contains = filter.Contains(item);

        // Assert
        Assert.True(added);
        Assert.True(contains);
    }

    [Fact]
    public async Task StringOverloads_Should_TranscodeAndExecute() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        string text = "user-identity-999";

        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        this._batch.StringGetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act
        bool asyncAdded = await filter.AddAsync(text);
        bool asyncContains = await filter.ContainsAsync(text);
        bool syncAdded = filter.Add(text.AsSpan());
        bool syncContains = filter.Contains(text.AsSpan());

        // Assert
        Assert.True(asyncAdded);
        Assert.True(asyncContains);
        Assert.True(syncAdded);
        Assert.True(syncContains);
    }

    [Fact]
    public async Task PopCount_Should_CallStringBitCount_OnAsyncAndSync() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        this._database.StringBitCountAsync(Arg.Any<RedisKey>(), 0, -1, CommandFlags.None)
            .Returns(Task.FromResult(42L));
        this._database.StringBitCount(Arg.Any<RedisKey>(), 0, -1, CommandFlags.None)
            .Returns(42L);

        // Act
        long asyncCount = await filter.GetPopCountAsync();
        long syncCount = filter.GetPopCount();

        // Assert
        Assert.Equal(42L, asyncCount);
        Assert.Equal(42L, syncCount);
    }

    [Fact]
    public async Task CustomKeyPrefixAndDatabase_Should_BeRespected() {
        // Arrange
        int targetDb = 3;
        DistributedRedisBloomFilter filter = CreateFilter(keyPrefix: "custom:bf:", database: targetDb);
        byte[] item = Encoding.UTF8.GetBytes("test");

        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act
        await filter.AddAsync(item);

        // Assert
        this._multiplexer.Received(1).GetDatabase(targetDb, Arg.Any<object>());
        await this._batch.Received().StringSetBitAsync(
            (RedisKey)"custom:bf:distributed-filter",
            Arg.Any<long>(),
            true,
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Cancellation_Should_ThrowOperationCanceledException() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        byte[] item = Encoding.UTF8.GetBytes("test");

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await filter.AddAsync(item, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await filter.AddAsync("test", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await filter.ContainsAsync(item, cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await filter.ContainsAsync("test", cts.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await filter.GetPopCountAsync(cts.Token));
    }

    [Fact]
    public void GenericWrapper_Should_ImplementGenericMarkerInterfaces() {
        // Arrange
        IOptions<DistributedBloomFilterOptions> options = Microsoft.Extensions.Options.Options.Create(new DistributedBloomFilterOptions());
        DistributedRedisBloomFilter<TestTag> typedFilter = new(this._multiplexer, this._config, options);

        // Assert
        Assert.IsAssignableFrom<IBloomFilter<TestTag>>(typedFilter);
        Assert.IsAssignableFrom<IAsyncBloomFilter<TestTag>>(typedFilter);
        Assert.IsAssignableFrom<IBloomFilter>(typedFilter);
        Assert.IsAssignableFrom<IAsyncBloomFilter>(typedFilter);
        Assert.Equal(this._config.Name, typedFilter.Name);
    }

    [Fact]
    public async Task LargePayload_And_EmptyPayload_Should_ExecuteSuccessfully() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] largeItem = new byte[10 * 1024]; // 10KB
        string largeString = new('A', 500); // 500 chars (exercises ArrayPool)

        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(false));
        this._batch.StringGetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), Arg.Any<CommandFlags>())
            .Returns(Task.FromResult(true));

        // Act & Assert
        Assert.True(await filter.AddAsync(largeItem));
        Assert.True(await filter.ContainsAsync(largeItem));

        Assert.True(filter.Add(largeString.AsSpan()));
        Assert.True(filter.Contains(largeString.AsSpan()));

        Assert.True(filter.Add(ReadOnlySpan<byte>.Empty));
        Assert.True(filter.Contains(ReadOnlySpan<byte>.Empty));

        Assert.True(filter.Add(string.Empty.AsSpan()));
        Assert.True(filter.Contains(string.Empty.AsSpan()));
    }

    [Fact]
    public async Task NullString_Should_ThrowArgumentNullException() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();

        // Act & Assert
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => filter.AddAsync((string)null!).AsTask());
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => filter.ContainsAsync((string)null!).AsTask());
    }

    [Fact]
    public async Task BitPositions_Should_AllBeWithinConfiguredSizeInBits() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("bounded-check");

        List<long> capturedOffsets = [];
        this._batch.StringSetBitAsync(
            Arg.Any<RedisKey>(),
            Arg.Do<long>(offset => capturedOffsets.Add(offset)),
            true,
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(false));

        // Act
        await filter.AddAsync(item);

        // Assert
        Assert.Equal(this._config.HashFunctionCount, capturedOffsets.Count);
        foreach (long offset in capturedOffsets) {
            Assert.InRange(offset, 0L, this._config.SizeInBits - 1);
        }
    }

    [Fact]
    public async Task BatchExecution_ThrowingException_Should_Propagate() {
        // Arrange
        DistributedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("fail-batch");

        this._batch.StringSetBitAsync(Arg.Any<RedisKey>(), Arg.Any<long>(), true, Arg.Any<CommandFlags>())
            .Returns(Task.FromException<bool>(new RedisServerException("Redis memory exhausted")));

        // Act & Assert
        await Assert.ThrowsAsync<RedisServerException>(async () => await filter.AddAsync(item));
    }
}
