using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting.Resilience;

namespace Wiaoj.RateLimiting.Tests.Unit.DependencyInjection;

public sealed class RateLimitingBuilderTests {
    [Fact]
    public void AddWiaojRateLimiting_WithFixedWindow_RegistersAlgorithm() {
        ServiceCollection services = new();
        services.AddDistributedCounter(b => b.UseInMemory());

        services.AddWiaojRateLimiting(rl => {
            rl.UseFixedWindow(limit: 10, window: TimeSpan.FromMinutes(1));
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IRateLimitAlgorithm algorithm = sp.GetRequiredService<IRateLimitAlgorithm>();

        Assert.IsType<FixedWindowRateLimiter>(algorithm);
    }

    [Fact]
    public void AddWiaojRateLimiting_WithNegativeCachingAndFailOpen_DecoratesCorrectly() {
        ServiceCollection services = new();
        services.AddDistributedCounter(b => b.UseInMemory());

        services.AddWiaojRateLimiting(rl => {
            rl.UseFixedWindow(limit: 10, window: TimeSpan.FromMinutes(1));
            rl.WithNegativeCaching();
            rl.WithFailOpen();
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IRateLimitAlgorithm algorithm = sp.GetRequiredService<IRateLimitAlgorithm>();

        // En dışta ResilientRateLimiter (Fail-Open), onun içinde NegativeCacheRateLimiter, en içte FixedWindow olmalı
        Assert.IsType<ResilientRateLimiter>(algorithm);
    }

    [Fact]
    public void AddWiaojRateLimiting_WithTokenBucket_RegistersTokenBucketAlgorithm() {
        ServiceCollection services = new();

        services.AddWiaojRateLimiting(rl => {
            rl.UseTokenBucket(capacity: 5, window: TimeSpan.FromSeconds(5));
        });

        ServiceProvider sp = services.BuildServiceProvider();
        IRateLimitAlgorithm algorithm = sp.GetRequiredService<IRateLimitAlgorithm>();

        Assert.IsType<TokenBucketRateLimiter>(algorithm);
    }
}