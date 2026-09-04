using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Redis.Engine;
using Wiaoj.BloomFilter.Redis.Options;
using Wiaoj.BloomFilter.Redis.Storage;

namespace Wiaoj.BloomFilter.Redis.Tests.Unit.DependencyInjection;

public class RedisBloomFilterRegistrationTests {
    private sealed record UserTag;
    private sealed record OrderTag;

    private readonly IConnectionMultiplexer _multiplexer;
    private readonly IDatabase _database;
    private readonly ISubscriber _subscriber;

    public RedisBloomFilterRegistrationTests() {
        this._multiplexer = Substitute.For<IConnectionMultiplexer>();
        this._database = Substitute.For<IDatabase>();
        this._subscriber = Substitute.For<ISubscriber>();

        this._multiplexer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(this._database);
        this._multiplexer.GetSubscriber(Arg.Any<object>()).Returns(this._subscriber);
    }

    [Fact]
    public void UseRedisStorage_Should_RegisterRedisBloomFilterStorage_WithExistingMultiplexer() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedisStorage();
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();

        // Assert
        Assert.NotNull(storage);
        Assert.IsType<RedisBloomFilterStorage>(storage);
    }

    [Fact]
    public void UseRedisStorage_WithMultiplexerAndOptions_Should_ConfigureStorage() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedisStorage(this._multiplexer, options => {
                options.KeyPrefix = "test:prefix:";
                options.EnableCompression = true;
                options.Ttl = TimeSpan.FromHours(1);
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IBloomFilterStorage storage = sp.GetRequiredService<IBloomFilterStorage>();
        IOptions<RedisBloomFilterStorageOptions> options = sp.GetRequiredService<IOptions<RedisBloomFilterStorageOptions>>();

        // Assert
        Assert.NotNull(storage);
        Assert.Equal("test:prefix:", options.Value.KeyPrefix);
        Assert.True(options.Value.EnableCompression);
        Assert.Equal(TimeSpan.FromHours(1), options.Value.Ttl);
    }

    [Fact]
    public void AddDistributedFilter_Should_RegisterTypedFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter<OrderTag>("orders", 5000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var syncFilter = sp.GetRequiredService<IBloomFilter<OrderTag>>();
        var asyncFilter = sp.GetRequiredService<IAsyncBloomFilter<OrderTag>>();

        // Assert
        Assert.NotNull(syncFilter);
        Assert.NotNull(asyncFilter);
        Assert.Same(syncFilter, asyncFilter);
        Assert.IsType<DistributedRedisBloomFilter<OrderTag>>(syncFilter);
        Assert.Equal("orders", syncFilter.Name.Value);
    }

    [Fact]
    public void AddDistributedFilter_WithOptions_Should_ApplyKeyPrefix() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter<OrderTag>("orders", 5000, 0.01, options => {
                options.KeyPrefix = "orders:dist:";
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IOptions<DistributedBloomFilterOptions> options = sp.GetRequiredService<IOptions<DistributedBloomFilterOptions>>();

        // Assert
        Assert.Equal("orders:dist:", options.Value.KeyPrefix);
    }

    [Fact]
    public void AddSynchronizedFilter_Should_RegisterTypedFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter<UserTag>("users", 1000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var syncFilter = sp.GetRequiredService<IBloomFilter<UserTag>>();
        var asyncFilter = sp.GetRequiredService<IAsyncBloomFilter<UserTag>>();

        // Assert
        Assert.NotNull(syncFilter);
        Assert.NotNull(asyncFilter);
        Assert.Same(syncFilter, asyncFilter);
        Assert.IsType<SynchronizedRedisBloomFilter<UserTag>>(syncFilter);
        Assert.Equal("users", syncFilter.Name.Value);
    }

    [Fact]
    public void UseRedis_WithMultiplexer_Should_RegisterMultiplexer() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedis(this._multiplexer);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IConnectionMultiplexer resolved = sp.GetRequiredService<IConnectionMultiplexer>();

        // Assert
        Assert.Same(this._multiplexer, resolved);
    }

    [Fact]
    public void AddMultipleDistributedFilters_WithDifferentTags_Should_ResolveIndependently() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter<UserTag>("users-dist", 10_000, 0.01);
            bf.AddDistributedFilter<OrderTag>("orders-dist", 20_000, 0.005);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var userFilter = sp.GetRequiredService<IBloomFilter<UserTag>>();
        var orderFilter = sp.GetRequiredService<IBloomFilter<OrderTag>>();

        // Assert
        Assert.NotNull(userFilter);
        Assert.NotNull(orderFilter);
        Assert.NotSame(userFilter, orderFilter);
        Assert.Equal("users-dist", userFilter.Name.Value);
        Assert.Equal("orders-dist", orderFilter.Name.Value);
        Assert.Equal(10_000, userFilter.Configuration.ExpectedItems);
        Assert.Equal(20_000, orderFilter.Configuration.ExpectedItems);
    }

    [Fact]
    public void AddMultipleSynchronizedFilters_WithDifferentTags_Should_ResolveIndependently() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter<UserTag>("users-sync", 5_000, 0.01);
            bf.AddSynchronizedFilter<OrderTag>("orders-sync", 15_000, 0.001);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var userFilter = sp.GetRequiredService<IBloomFilter<UserTag>>();
        var orderFilter = sp.GetRequiredService<IBloomFilter<OrderTag>>();

        // Assert
        Assert.NotNull(userFilter);
        Assert.NotNull(orderFilter);
        Assert.NotSame(userFilter, orderFilter);
        Assert.Equal("users-sync", userFilter.Name.Value);
        Assert.Equal("orders-sync", orderFilter.Name.Value);
    }

    [Fact]
    public void AddDistributedFilter_WithoutMultiplexer_Should_ThrowInvalidOperationException_WhenResolved() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter<UserTag>("users", 1000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IBloomFilter<UserTag>>());
    }

    [Fact]
    public void AddSynchronizedFilter_WithoutMultiplexer_Should_ThrowInvalidOperationException_WhenResolved() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter<UserTag>("users", 1000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IBloomFilter<UserTag>>());
    }
}
