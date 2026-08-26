using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Feature", "PolicyStorageRouting")]
public sealed class RateLimiterPolicyStorageRoutingTests {

    [Fact]
    public async Task PolicyWithKeyedStorage_RoutesCounterOperationsToDedicatedKeyedStorage() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage keyedStorage = new();

        // 1. Register global default storage and keyed storage double in DI
        services.AddDistributedCounter(dc => {
            dc.Services.AddSingleton<ICounterStorage>(defaultStorage);
        });

        services.AddKeyedSingleton<ICounterStorage>("security-storage", keyedStorage);

        // 2. Configure policies: "auth" -> keyed storage, "general" -> default storage
        services.AddWiaojRateLimiting(limiter => {
            limiter.AddPolicy("auth", policy => {
                policy.UseFixedWindow(limit: 5, window: TimeSpan.FromMinutes(1))
                      .UseKeyedStorage("security-storage");
            });

            limiter.AddPolicy("general", policy => {
                policy.UseFixedWindow(limit: 10, window: TimeSpan.FromMinutes(1));
            });
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IRateLimiter rateLimiter = provider.GetRequiredService<IRateLimiter>();
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act 1: Acquire against "auth" policy (should hit keyedStorage)
        RateLimitDecision authDecision = await rateLimiter.TryAcquireAsync("auth", "user_123", ct);

        // Act 2: Acquire against "general" policy (should hit defaultStorage)
        RateLimitDecision generalDecision = await rateLimiter.TryAcquireAsync("general", "user_123", ct);

        // Assert: Both allowed
        Assert.True(authDecision.IsAllowed);
        Assert.True(generalDecision.IsAllowed);

        // Assert: "auth" hit ONLY keyedStorage
        Assert.Equal(1, keyedStorage.TryIncrementCallCount);
        Assert.True(keyedStorage.Snapshot.ContainsKey("wiaoj:counter:auth:user_123"));
        Assert.False(keyedStorage.Snapshot.ContainsKey("wiaoj:counter:general:user_123"));

        // Assert: "general" hit ONLY defaultStorage
        Assert.Equal(1, defaultStorage.TryIncrementCallCount);
        Assert.True(defaultStorage.Snapshot.ContainsKey("wiaoj:counter:general:user_123"));
        Assert.False(defaultStorage.Snapshot.ContainsKey("wiaoj:counter:auth:user_123"));
    }

    [Fact]
    public async Task PolicyWithCustomStorageFactory_RoutesCounterOperationsToFactoryResolvedStorage() {
        // Arrange
        ServiceCollection services = new();
        FakeCounterStorage defaultStorage = new();
        FakeCounterStorage dedicatedCustomStorage = new();

        services.AddDistributedCounter(dc => {
            dc.Services.AddSingleton<ICounterStorage>(defaultStorage);
        });

        services.AddWiaojRateLimiting(limiter => {
            limiter.AddPolicy("billing", policy => {
                policy.UseGcra(limit: 10, period: TimeSpan.FromSeconds(10))
                      .UseStorage(_ => dedicatedCustomStorage);
            });
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        IRateLimiter rateLimiter = provider.GetRequiredService<IRateLimiter>();
        CancellationToken ct = TestContext.Current.CancellationToken;

        // Act
        RateLimitDecision decision = await rateLimiter.TryAcquireAsync("billing", "customer_99", ct);

        // Assert
        Assert.True(decision.IsAllowed);
        Assert.True(dedicatedCustomStorage.Snapshot.ContainsKey("wiaoj:counter:billing:customer_99"));
        Assert.Empty(defaultStorage.Snapshot); // Default storage completely untouched
    }
}