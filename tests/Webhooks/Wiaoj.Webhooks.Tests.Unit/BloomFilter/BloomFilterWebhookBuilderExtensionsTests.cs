using Microsoft.Extensions.DependencyInjection;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Webhooks.BloomFilter;

namespace Wiaoj.Webhooks.Tests.Unit.BloomFilter;

[Trait("Category", "Unit")]
[Trait("Feature", "DependencyInjection")]
[Trait("Component", "BloomFilter")]
public sealed class BloomFilterWebhookBuilderExtensionsTests {

    [Fact]
    public void UseBloomFilterDeduplication_WithInstance_RegistersSuccessfully() {
        ServiceCollection services = new();
        FakeBloomFilter filter = new("test-filter");

        services.AddLogging();
        services.AddWiaojWebhooks(webhooks => {
            webhooks.UseBloomFilterDeduplication(filter);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var middleware = sp.GetRequiredService<BloomFilterDeduplicationMiddleware>();

        Assert.NotNull(middleware);
        Assert.Same(filter, sp.GetRequiredService<IBloomFilter>());
    }

    [Fact]
    public void UseBloomFilterDeduplication_WithKeyedName_ResolvesKeyedBloomFilter() {
        ServiceCollection services = new();
        FakeBloomFilter filter = new("keyed-filter");

        services.AddLogging();
        services.AddKeyedSingleton<IBloomFilter>("keyed-filter", filter);
        services.AddWiaojWebhooks(webhooks => {
            webhooks.UseBloomFilterDeduplication("keyed-filter");
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var middleware = sp.GetRequiredService<BloomFilterDeduplicationMiddleware>();

        Assert.NotNull(middleware);
    }

    [Fact]
    public void UseBloomFilterDeduplication_WithOptionsAction_ConfiguresOptionsProperly() {
        ServiceCollection services = new();
        FakeBloomFilter filter = new("configured-filter");

        services.AddLogging();
        services.AddWiaojWebhooks(webhooks => {
            webhooks.UseBloomFilterDeduplication(filter, options => {
                options.Capacity = 250_000;
                options.ErrorRate = 0.05;
            });
        });

        ServiceProvider sp = services.BuildServiceProvider();
        var options = sp.GetRequiredService<BloomFilterDeduplicationOptions>();

        Assert.Equal(250_000, options.Capacity);
        Assert.Equal(0.05, options.ErrorRate);
    }
}