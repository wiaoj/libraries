using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Wiaoj.BloomFilter.Engine;
using Xunit;

namespace Wiaoj.BloomFilter.Tests.Unit.DependencyInjection;

public sealed class ShardedBloomFilterRegistrationTests {
    private sealed record ShardedOrdersTag;

    [Fact]
    public void Should_RegisterShardedFilter_WithExplicitShardCount_When_OverloadUsed() {
        ServiceCollection services = new();
        services.AddBloomFilter(builder => {
            builder.AddShardedFilter<ShardedOrdersTag>("sharded-orders", expectedItems: 1_000_000, errorRate: 0.01, shardCount: 8);
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<BloomFilterOptions> options = sp.GetRequiredService<IOptions<BloomFilterOptions>>();

        Assert.True(options.Value.Filters.TryGetValue("sharded-orders", out FilterDefinition? def));
        Assert.NotNull(def);
        Assert.Equal(BloomFilterType.Sharded, def.Type);
        Assert.Equal(1_000_000, def.ExpectedItems);
        Assert.Equal(0.01, def.ErrorRate);
        Assert.Equal(8, def.ShardCount);

        // Resolve filter instance and verify it is a ShardedBloomFilter
        IBloomFilter<ShardedOrdersTag> filter = sp.GetRequiredService<IBloomFilter<ShardedOrdersTag>>();
        Assert.NotNull(filter);
    }

    [Fact]
    public void Should_RegisterShardedFilter_WithAutoOptimalShardCount_When_ShardCountOmitted() {
        ServiceCollection services = new();
        services.AddBloomFilter(builder => {
            builder.AddShardedFilter("auto-sharded-payments", expectedItems: 500_000, errorRate: 0.001);
        });

        using ServiceProvider sp = services.BuildServiceProvider();
        IOptions<BloomFilterOptions> options = sp.GetRequiredService<IOptions<BloomFilterOptions>>();

        Assert.True(options.Value.Filters.TryGetValue("auto-sharded-payments", out FilterDefinition? def));
        Assert.NotNull(def);
        Assert.Equal(BloomFilterType.Sharded, def.Type);
        Assert.Equal(500_000, def.ExpectedItems);
        Assert.Equal(0.001, def.ErrorRate);
        Assert.Equal(0, def.ShardCount); // 0 means auto-calculate optimal shard count
    }

    [Fact]
    public void Should_ThrowArgumentException_When_AddShardedFilterReceivesInvalidShardCount() {
        ServiceCollection services = new();
        IBloomFilterBuilder builder = null!;
        services.AddBloomFilter(b => builder = b);

        Assert.NotNull(builder);
        Assert.ThrowsAny<ArgumentException>(() => builder.AddShardedFilter("bad-1", 10_000, 0.01, shardCount: 1));
        Assert.ThrowsAny<ArgumentException>(() => builder.AddShardedFilter("bad-3", 10_000, 0.01, shardCount: 3));
        Assert.ThrowsAny<ArgumentException>(() => builder.AddShardedFilter("bad-6", 10_000, 0.01, shardCount: 6));
    }
}
