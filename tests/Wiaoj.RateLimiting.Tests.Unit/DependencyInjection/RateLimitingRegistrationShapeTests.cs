using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
public sealed class RateLimitingRegistrationShapeTests {
    [Fact]
    public async Task Fluent_Overload_Registers_Policies_On_The_Returned_Builder() {
        ServiceCollection services = new();
        services.AddDistributedCounter(dc => dc.UseInMemory());

        services.AddWiaojRateLimiting()
            .AddPolicy("login", policy => policy.UseFixedWindow(limit: 1, window: TimeSpan.FromMinutes(1)));

        using ServiceProvider provider = services.BuildServiceProvider();
        IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();
        CancellationToken ct = TestContext.Current.CancellationToken;

        Assert.True((await limiter.TryAcquireAsync("login", "client", ct)).IsAllowed);
        Assert.False((await limiter.TryAcquireAsync("login", "client", ct)).IsAllowed);
    }

    [Fact]
    public void Callback_Overload_Returns_The_Service_Collection() {
        ServiceCollection services = new();

        Assert.Same(services, services.AddWiaojRateLimiting(_ => { }));
    }
}
