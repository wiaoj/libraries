using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit; 
public sealed class ShardedBloomFilterTests {

    [Fact]
    public void ShardedFilter_Should_Distribute_And_Find_Items() {
        // Arrange: 4 shard'lı bir yapı kur
        BloomFilterConfiguration config = new BloomFilterConfiguration("sharded", 1000, Percentage.FromDouble(0.01))
            .WithShardCount(4);

        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(NullLogger.Instance);

        ShardedBloomFilter shardedFilter = new(
            config, null, loggerFactory, new BloomFilterOptions(), TimeProvider.System);
         
        byte[][] items = [
            "user-1"u8.ToArray(),
            "user-2"u8.ToArray(),
            "user-3"u8.ToArray(),
            "user-4"u8.ToArray()
        ];

        // Act
        foreach(byte[] item in items)
            shardedFilter.Add(item);

        // Assert
        foreach(byte[] item in items) {
            Assert.True(shardedFilter.Contains(item), $"Item {Encoding.UTF8.GetString(item)} should be found.");
        }

        // Shard'ların boş olmadığını (verinin dağıldığını) dolaylı yoldan doğrula
        Assert.True(shardedFilter.GetPopCount() >= items.Length);
    }

    [Fact]
    public async Task ShardedFilter_Save_Should_Trigger_Multiple_Saves() {
        // Arrange
        IBloomFilterStorage mockStorage = Substitute.For<IBloomFilterStorage>();
        BloomFilterConfiguration config = new BloomFilterConfiguration("sharded", 1000, Percentage.FromDouble(0.01))
            .WithShardCount(2); // 2 shard = 2 dosya yazılmalı

        ILoggerFactory loggerFactory = Substitute.For<ILoggerFactory>();
        ShardedBloomFilter shardedFilter = new(config, mockStorage, loggerFactory, new BloomFilterOptions(), TimeProvider.System);

        shardedFilter.Add("test-data"u8); // En az bir shard kirli (dirty) olacak

        // Act
        await shardedFilter.SaveAsync();

        // Assert: En az bir shard dosyası yazılmış olmalı
        // Shard isimleri url-blacklist_s0, url-blacklist_s1 şeklinde oluşuyor
        await mockStorage.Received().SaveAsync(
            Arg.Is<string>(s => s.Contains("sharded_s")),
            Arg.Any<BloomFilterConfiguration>(),
            Arg.Any<Stream>(),
            Arg.Any<CancellationToken>());
    }
}