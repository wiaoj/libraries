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

    [Fact]
    public void AddMultipleDistributedFilters_WithOptions_Should_HaveIsolatedOptionsPerFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter<UserTag>("users-dist", 10_000, 0.01, opt => {
                opt.KeyPrefix = "users:";
                opt.Database = 1;
            });
            bf.AddDistributedFilter<OrderTag>("orders-dist", 20_000, 0.005, opt => {
                opt.KeyPrefix = "orders:";
                opt.Database = 2;
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<IBloomFilter<UserTag>>();
        _ = sp.GetRequiredService<IBloomFilter<OrderTag>>();

        IOptionsMonitor<DistributedBloomFilterOptions> monitor = sp.GetRequiredService<IOptionsMonitor<DistributedBloomFilterOptions>>();

        // Assert - Both filters must have their own isolated options, not overwritten
        Assert.Equal("users:", monitor.Get("users-dist").KeyPrefix);
        Assert.Equal(1, monitor.Get("users-dist").Database);

        Assert.Equal("orders:", monitor.Get("orders-dist").KeyPrefix);
        Assert.Equal(2, monitor.Get("orders-dist").Database);
    }

    [Fact]
    public void AddMultipleSynchronizedFilters_WithOptions_Should_HaveIsolatedOptionsPerFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        Guid userNodeId = Guid.NewGuid();
        Guid orderNodeId = Guid.NewGuid();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter<UserTag>("users-sync", 5_000, 0.01, opt => {
                opt.SyncChannelPrefix = "users:sync:";
                opt.NodeId = userNodeId;
            });
            bf.AddSynchronizedFilter<OrderTag>("orders-sync", 15_000, 0.001, opt => {
                opt.SyncChannelPrefix = "orders:sync:";
                opt.NodeId = orderNodeId;
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();
        _ = sp.GetRequiredService<IBloomFilter<UserTag>>();
        _ = sp.GetRequiredService<IBloomFilter<OrderTag>>();

        IOptionsMonitor<SynchronizedBloomFilterOptions> monitor = sp.GetRequiredService<IOptionsMonitor<SynchronizedBloomFilterOptions>>();

        // Assert - Both filters must have isolated options
        Assert.Equal("users:sync:", monitor.Get("users-sync").SyncChannelPrefix);
        Assert.Equal(userNodeId, monitor.Get("users-sync").NodeId);

        Assert.Equal("orders:sync:", monitor.Get("orders-sync").SyncChannelPrefix);
        Assert.Equal(orderNodeId, monitor.Get("orders-sync").NodeId);
    }

    [Fact]
    public void AddDistributedFilter_NonGeneric_Should_RegisterKeyedFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddDistributedFilter("non-generic-dist", 5_000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var filter = sp.GetRequiredKeyedService<IBloomFilter>("non-generic-dist");
        var asyncFilter = sp.GetRequiredKeyedService<IAsyncBloomFilter>("non-generic-dist");

        // Assert
        Assert.NotNull(filter);
        Assert.NotNull(asyncFilter);
        Assert.Same(filter, asyncFilter);
        Assert.Equal("non-generic-dist", filter.Name.Value);
    }

    [Fact]
    public void AddSynchronizedFilter_NonGeneric_Should_RegisterKeyedFilter() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter("non-generic-sync", 5_000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var filter = sp.GetRequiredKeyedService<IBloomFilter>("non-generic-sync");
        var asyncFilter = sp.GetRequiredKeyedService<IAsyncBloomFilter>("non-generic-sync");
        var persistentFilter = sp.GetRequiredKeyedService<IPersistentBloomFilter>("non-generic-sync");

        // Assert
        Assert.NotNull(filter);
        Assert.NotNull(asyncFilter);
        Assert.NotNull(persistentFilter);
        Assert.Same(filter, asyncFilter);
        Assert.Same(filter, persistentFilter);
        Assert.Equal("non-generic-sync", filter.Name.Value);
    }

    [Fact]
    public void AddSynchronizedFilter_Should_RegisterFilterInBloomFilterRegistry() {
        // Arrange
        ServiceCollection services = new();
        services.AddSingleton(this._multiplexer);
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.AddSynchronizedFilter<UserTag>("registered-user", 1_000, 0.01);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var filter = sp.GetRequiredService<IBloomFilter<UserTag>>();
        IBloomFilterRegistry registry = sp.GetRequiredService<IBloomFilterRegistry>();

        // Assert - Registry must contain the filter for AutoSaveService to persist it
        var allFilters = registry.GetAll().ToList();
        Assert.Contains(allFilters, f => f.Name.Value == "registered-user");
    }

    [Fact]
    public void UseRedis_WithConnectionString_Should_RegisterMultiplexerDescriptor() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedis("localhost:6379");
        });

        // Assert - Service descriptor for IConnectionMultiplexer is registered as Singleton
        ServiceDescriptor? descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConnectionMultiplexer));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseRedis_WithConfigurationOptions_Should_RegisterMultiplexerDescriptor() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();
        ConfigurationOptions redisOptions = new() { EndPoints = { "localhost:6379" } };

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedis(redisOptions);
        });

        // Assert
        ServiceDescriptor? descriptor = services.FirstOrDefault(sd => sd.ServiceType == typeof(IConnectionMultiplexer));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void UseRedisStorage_WithConnectionString_Should_RegisterMultiplexerAndStorage() {
        // Arrange
        ServiceCollection services = new();
        services.AddLogging();

        // Act
        services.AddBloomFilter(bf => {
            bf.UseRedisStorage("localhost:6379", opt => {
                opt.KeyPrefix = "custom:storage:";
            });
        });

        // Assert
        Assert.Contains(services, sd => sd.ServiceType == typeof(IConnectionMultiplexer));
        Assert.Contains(services, sd => sd.ServiceType == typeof(IBloomFilterStorage) && sd.ImplementationType == typeof(RedisBloomFilterStorage));
    }

    [Fact]
    public void UseRedis_Should_ThrowOnNullOrInvalidArguments() {
        ServiceCollection services = new();
        services.AddBloomFilter(bf => {
            Assert.ThrowsAny<ArgumentNullException>(() => bf.UseRedis((string)null!));
            Assert.ThrowsAny<ArgumentException>(() => bf.UseRedis("   "));
            Assert.ThrowsAny<ArgumentNullException>(() => bf.UseRedis((ConfigurationOptions)null!));
            Assert.ThrowsAny<ArgumentNullException>(() => bf.UseRedis((IConnectionMultiplexer)null!));
            Assert.ThrowsAny<ArgumentNullException>(() => bf.UseRedisStorage((string)null!));
            Assert.ThrowsAny<ArgumentException>(() => bf.UseRedisStorage("   "));
            Assert.ThrowsAny<ArgumentNullException>(() => bf.UseRedisStorage((IConnectionMultiplexer)null!));
        });
    }
}

