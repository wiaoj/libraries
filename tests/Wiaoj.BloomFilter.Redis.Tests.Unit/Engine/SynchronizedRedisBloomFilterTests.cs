using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IO;
using NSubstitute;
using StackExchange.Redis;
using System.Text;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Engine;
using Wiaoj.BloomFilter.Redis.Messaging;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.BloomFilter.Testing;

namespace Wiaoj.BloomFilter.Redis.Tests.Unit.Engine;

public class SynchronizedRedisBloomFilterTests {
    private sealed record TestTag;

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly ISubscriber _subscriber;
    private readonly BloomFilterConfiguration _config;
    private readonly FakeBloomFilterStorage _storage;

    public SynchronizedRedisBloomFilterTests() {
        this._multiplexer = Substitute.For<IConnectionMultiplexer>();
        this._subscriber = Substitute.For<ISubscriber>();
        this._multiplexer.GetSubscriber(Arg.Any<object>()).Returns(this._subscriber);

        this._config = new BloomFilterConfiguration(
            FilterName.Parse("hybrid-filter"),
            expectedItems: 1000,
            errorRate: 0.01,
            sizeInBits: 10000,
            hashFunctionCount: 5,
            hashSeed: 42);

        this._storage = new FakeBloomFilterStorage();
    }

    private InMemoryBloomFilter CreateInMemoryFilter() {
        BloomFilterContext context = new(
            Storage: this._storage,
            RecyclableMemoryStreamManager: new RecyclableMemoryStreamManager(),
            Logger: NullLogger.Instance,
            Options: new BloomFilterOptions(),
            TimeProvider: TimeProvider.System,
            ConfigFactory: new BloomFilterConfigurationFactory()
        );

        return new InMemoryBloomFilter(this._config, context);
    }

    private SynchronizedRedisBloomFilter CreateFilter(
        Guid? nodeId = null,
        string channelPrefix = "bloom:sync:",
        bool enableSnapshotPersistence = true) {
        SynchronizedBloomFilterOptions options = new() {
            SyncChannelPrefix = channelPrefix,
            NodeId = nodeId,
            EnableSnapshotPersistence = enableSnapshotPersistence
        };

        InMemoryBloomFilter innerFilter = CreateInMemoryFilter();

        return new SynchronizedRedisBloomFilter(
            this._multiplexer,
            innerFilter,
            Microsoft.Extensions.Options.Options.Create(options));
    }

    [Fact]
    public void BloomFilterSyncMessage_Should_SerializeAndDeserialize_Correctly() {
        // Arrange
        Guid nodeId = Guid.NewGuid();
        ulong h1 = 0x0123456789ABCDEFUL;
        ulong h2 = 0xFEDCBA9876543210UL;
        BloomFilterSyncMessage message = new(nodeId, h1, h2);

        // Act
        byte[] bytes = message.ToByteArray();
        bool parsed = BloomFilterSyncMessage.TryParse(bytes, out BloomFilterSyncMessage result);

        // Assert
        Assert.True(parsed);
        Assert.Equal(32, bytes.Length);
        Assert.Equal(nodeId, result.OriginNodeId);
        Assert.Equal(h1, result.Hash1);
        Assert.Equal(h2, result.Hash2);
    }

    [Fact]
    public void BloomFilterSyncMessage_Should_FailToParse_WhenLengthIsInvalid() {
        byte[] tooShort = new byte[31];
        byte[] tooLong = new byte[33];

        Assert.False(BloomFilterSyncMessage.TryParse(tooShort, out _));
        Assert.False(BloomFilterSyncMessage.TryParse(tooLong, out _));
        Assert.False(BloomFilterSyncMessage.TryParse([], out _));
    }

    [Fact]
    public void Constructor_Should_SubscribeToChannel() {
        // Arrange & Act
        using SynchronizedRedisBloomFilter filter = CreateFilter(channelPrefix: "custom:channel:");

        // Assert
        this._subscriber.Received(1).Subscribe(
            RedisChannel.Literal("custom:channel:hybrid-filter"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Add_Should_MutateLocalMemory_And_PublishSyncMessage() {
        // Arrange
        Guid nodeId = Guid.NewGuid();
        using SynchronizedRedisBloomFilter filter = CreateFilter(nodeId: nodeId);
        byte[] item = Encoding.UTF8.GetBytes("item-to-replicate");

        byte[]? publishedPayload = null;
        this._subscriber.PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Do<RedisValue>(v => publishedPayload = (byte[])v!),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(1L));

        // Act
        bool added = filter.Add(item);

        // Assert
        Assert.True(added);
        Assert.True(filter.Contains(item)); // Local SIMD memory updated instantly
        Assert.NotNull(publishedPayload);
        Assert.True(BloomFilterSyncMessage.TryParse(publishedPayload, out BloomFilterSyncMessage syncMsg));
        Assert.Equal(nodeId, syncMsg.OriginNodeId);

        await this._subscriber.Received(1).PublishAsync(
            RedisChannel.Literal("bloom:sync:hybrid-filter"),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public async Task Add_Should_NotPublish_WhenItemAlreadyPresent() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("duplicate-item");

        this._subscriber.PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(1L));

        // Act
        bool firstAdd = filter.Add(item);
        this._subscriber.ClearReceivedCalls();

        bool secondAdd = filter.Add(item);

        // Assert
        Assert.True(firstAdd);
        Assert.False(secondAdd);
        await this._subscriber.DidNotReceiveWithAnyArgs().PublishAsync(default, default);
    }

    [Fact]
    public void IncomingMessage_FromPeerNode_Should_UpdateLocalFilter() {
        // Arrange
        Action<RedisChannel, RedisValue>? messageHandler = null;
        this._subscriber.Subscribe(
            Arg.Any<RedisChannel>(),
            Arg.Do<Action<RedisChannel, RedisValue>>(h => messageHandler = h),
            Arg.Any<CommandFlags>());

        Guid myNodeId = Guid.NewGuid();
        using SynchronizedRedisBloomFilter filter = CreateFilter(nodeId: myNodeId);
        Assert.NotNull(messageHandler);

        byte[] item = Encoding.UTF8.GetBytes("peer-inserted-item");
        Assert.False(filter.Contains(item));

        BloomHasher.ComputeBaseHashes(item, this._config.HashSeed, out ulong h1, out ulong h2);
        Guid peerNodeId = Guid.NewGuid();
        BloomFilterSyncMessage peerMessage = new(peerNodeId, h1, h2);

        // Act - Simulate peer incoming message over Pub/Sub
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), peerMessage.ToByteArray());

        // Assert - Local filter now sees the element
        Assert.True(filter.Contains(item));
    }

    [Fact]
    public void IncomingMessage_FromSelf_Should_BeIgnored() {
        // Arrange
        Action<RedisChannel, RedisValue>? messageHandler = null;
        this._subscriber.Subscribe(
            Arg.Any<RedisChannel>(),
            Arg.Do<Action<RedisChannel, RedisValue>>(h => messageHandler = h),
            Arg.Any<CommandFlags>());

        Guid myNodeId = Guid.NewGuid();
        using SynchronizedRedisBloomFilter filter = CreateFilter(nodeId: myNodeId);
        Assert.NotNull(messageHandler);

        byte[] item = Encoding.UTF8.GetBytes("self-item");
        BloomHasher.ComputeBaseHashes(item, this._config.HashSeed, out ulong h1, out ulong h2);
        BloomFilterSyncMessage selfMessage = new(myNodeId, h1, h2);

        // Act - Deliver echo message from self
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), selfMessage.ToByteArray());

        // Assert
        Assert.False(filter.Contains(item));
    }

    [Fact]
    public void Dispose_Should_UnsubscribeFromRedis() {
        // Arrange
        SynchronizedRedisBloomFilter filter = CreateFilter();

        // Act
        filter.Dispose();

        // Assert
        this._subscriber.Received(1).Unsubscribe(
            RedisChannel.Literal("bloom:sync:hybrid-filter"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void GenericTypedFilter_Should_ImplementGenericMarkerInterfaces() {
        // Arrange
        SynchronizedBloomFilterOptions options = new();
        InMemoryBloomFilter inner = CreateInMemoryFilter();
        using SynchronizedRedisBloomFilter<TestTag> typedFilter = new(
            this._multiplexer,
            inner,
            Microsoft.Extensions.Options.Options.Create(options));

        // Assert
        Assert.IsAssignableFrom<IBloomFilter<TestTag>>(typedFilter);
        Assert.IsAssignableFrom<IAsyncBloomFilter<TestTag>>(typedFilter);
        Assert.IsAssignableFrom<IPersistentBloomFilter>(typedFilter);
        Assert.Equal(this._config.Name, typedFilter.Name);
    }

    [Fact]
    public void PublishAsync_Failure_Should_NotPrevent_LocalAdd_FromSucceeding() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("resilient-item");

        this._subscriber.PublishAsync(
            Arg.Any<RedisChannel>(),
            Arg.Any<RedisValue>(),
            Arg.Any<CommandFlags>()).Returns<Task<long>>(_ => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis Pub/Sub unreachable"));

        // Act
        bool added = filter.Add(item);

        // Assert - Local memory addition succeeded despite Redis publish error
        Assert.True(added);
        Assert.True(filter.Contains(item));
    }

    [Fact]
    public void IncomingMessage_WithCorruptedPayload_Should_NotCrash_Subscriber() {
        // Arrange
        Action<RedisChannel, RedisValue>? messageHandler = null;
        this._subscriber.Subscribe(
            Arg.Any<RedisChannel>(),
            Arg.Do<Action<RedisChannel, RedisValue>>(h => messageHandler = h),
            Arg.Any<CommandFlags>());

        using SynchronizedRedisBloomFilter filter = CreateFilter();
        Assert.NotNull(messageHandler);

        // Act & Assert - corrupt payload with 15 bytes should be caught and logged, not crash
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), new byte[15]);
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), RedisValue.EmptyString);
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), RedisValue.Null);
    }

    [Fact]
    public void MultiplePeerNodes_Should_SyncSimultaneously() {
        // Arrange
        Action<RedisChannel, RedisValue>? messageHandler = null;
        this._subscriber.Subscribe(
            Arg.Any<RedisChannel>(),
            Arg.Do<Action<RedisChannel, RedisValue>>(h => messageHandler = h),
            Arg.Any<CommandFlags>());

        Guid myNodeId = Guid.NewGuid();
        using SynchronizedRedisBloomFilter filter = CreateFilter(nodeId: myNodeId);
        Assert.NotNull(messageHandler);

        byte[] item1 = Encoding.UTF8.GetBytes("peer1-item");
        byte[] item2 = Encoding.UTF8.GetBytes("peer2-item");

        BloomHasher.ComputeBaseHashes(item1, this._config.HashSeed, out ulong h1A, out ulong h2A);
        BloomHasher.ComputeBaseHashes(item2, this._config.HashSeed, out ulong h1B, out ulong h2B);

        BloomFilterSyncMessage peer1Msg = new(Guid.NewGuid(), h1A, h2A);
        BloomFilterSyncMessage peer2Msg = new(Guid.NewGuid(), h1B, h2B);

        // Act
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), peer1Msg.ToByteArray());
        messageHandler.Invoke(RedisChannel.Literal("bloom:sync:hybrid-filter"), peer2Msg.ToByteArray());

        // Assert
        Assert.True(filter.Contains(item1));
        Assert.True(filter.Contains(item2));
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_Should_BeIdempotent() {
        // Arrange
        SynchronizedRedisBloomFilter filter = CreateFilter();

        // Act & Assert
        filter.Dispose();
        filter.Dispose(); // Should not throw

        this._subscriber.Received(1).Unsubscribe(
            RedisChannel.Literal("bloom:sync:hybrid-filter"),
            Arg.Any<Action<RedisChannel, RedisValue>>(),
            Arg.Any<CommandFlags>());
    }

    [Fact]
    public void Operations_AfterDispose_Should_ThrowObjectDisposedException() {
        // Arrange
        SynchronizedRedisBloomFilter filter = CreateFilter();
        filter.Dispose();

        byte[] item = [1, 2, 3];

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => filter.Add(item));
        Assert.Throws<ObjectDisposedException>(() => filter.Contains(item));
        Assert.Throws<ObjectDisposedException>(() => filter.Add("test".AsSpan()));
        Assert.Throws<ObjectDisposedException>(() => filter.Contains("test".AsSpan()));
        Assert.Throws<ObjectDisposedException>(() => filter.GetPopCount());

        Assert.Throws<ObjectDisposedException>(() => filter.AddAsync(item).AsTask().GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => filter.ContainsAsync(item).AsTask().GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => filter.GetPopCountAsync().AsTask().GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => filter.SaveAsync().AsTask().GetAwaiter().GetResult());
        Assert.Throws<ObjectDisposedException>(() => filter.ReloadAsync().AsTask().GetAwaiter().GetResult());
    }

    [Fact]
    public async Task When_EnableSnapshotPersistenceIsFalse_SaveAsyncAndReloadAsync_Should_BeNoOp_And_IsDirtyShouldBeFalse() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter(enableSnapshotPersistence: false);
        byte[] item = Encoding.UTF8.GetBytes("persisted-item");
        filter.Add(item);

        // Assert - Even after Add(), IsDirty must be false when persistence is disabled
        Assert.False(filter.IsDirty);

        // Act - SaveAsync and ReloadAsync should be no-ops and not commit to storage
        await filter.SaveAsync();
        Assert.False(this._storage.Exists(filter.Name));

        await filter.ReloadAsync();
        Assert.False(this._storage.Exists(filter.Name));
    }

    [Fact]
    public async Task When_EnableSnapshotPersistenceIsTrue_SaveAsyncAndReloadAsync_Should_DelegateToInnerFilter() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter(enableSnapshotPersistence: true);
        byte[] item = Encoding.UTF8.GetBytes("persisted-item");
        filter.Add(item);

        // Assert - IsDirty must be true after addition
        Assert.True(filter.IsDirty);

        // Act - Save snapshot
        await filter.SaveAsync();

        // Assert - Snapshot committed to storage, IsDirty cleared
        Assert.True(this._storage.Exists(filter.Name));
        Assert.False(filter.IsDirty);

        // Act - Reload snapshot
        await filter.ReloadAsync();
        Assert.True(filter.Contains(item));
    }

    [Fact]
    public async Task AddAsyncAndContainsAsync_MemoryOverloads_Should_OperateCorrectly() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter();
        byte[] item = Encoding.UTF8.GetBytes("memory-item");
        ReadOnlyMemory<byte> memory = item;

        // Act
        bool added = await filter.AddAsync(memory);
        bool contains = await filter.ContainsAsync(memory);

        // Assert
        Assert.True(added);
        Assert.True(contains);
    }

    [Fact]
    public async Task StringOverloads_And_LargeStrings_Should_OperateCorrectly() {
        // Arrange
        using SynchronizedRedisBloomFilter filter = CreateFilter();
        string smallString = "small-key";
        string largeString = new('x', 1000); // Exceeds 256 bytes, exercises ArrayPool path

        // Act
        bool addedSmall = await filter.AddAsync(smallString);
        bool containsSmall = await filter.ContainsAsync(smallString);

        bool addedLarge = filter.Add(largeString.AsSpan());
        bool containsLarge = filter.Contains(largeString.AsSpan());

        bool addedEmpty = filter.Add(string.Empty.AsSpan());
        bool containsEmpty = filter.Contains(string.Empty.AsSpan());

        // Assert
        Assert.True(addedSmall);
        Assert.True(containsSmall);
        Assert.True(addedLarge);
        Assert.True(containsLarge);
        Assert.True(addedEmpty);
        Assert.True(containsEmpty);
    }
}

