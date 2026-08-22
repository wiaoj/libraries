using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Webhooks.BloomFilter;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.BloomFilter;

[Trait("Category", "Unit")]
[Trait("Feature", "Deduplication")]
[Trait("Component", "BloomFilter")]
public sealed class BloomFilterDeduplicationTests {

    [Fact]
    public async Task InvokeAsync_PassesFirstEvent_AndBlocksDuplicateEvent() {
        FakeBloomFilter filter = new("test-dedup");
        BloomFilterDeduplicationOptions options = new();
        BloomFilterDeduplicationMiddleware middleware = new(filter, options, NullLogger<BloomFilterDeduplicationMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        int downstreamCallCount = 0;
        WebhookDelegate next = (ctx, ct) => {
            downstreamCallCount++;
            ctx.SetResult(WebhookDeliveryResult.Success(200, "OK"));
            return Task.CompletedTask;
        };

        // 1st delivery -> must pass through
        await middleware.InvokeAsync(context, next);
        Assert.Equal(1, downstreamCallCount);

        // 2nd delivery with same context -> must be blocked by BloomFilter deduplication
        await middleware.InvokeAsync(context, next);
        Assert.Equal(1, downstreamCallCount); // Downstream NOT called again

        Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
        WebhookDeliveryResult.Deduplicated dedup = Assert.IsType<WebhookDeliveryResult.Deduplicated>(result);
        Assert.True(dedup.IsSuccess);
        Assert.Equal(options.KeySelector(context), dedup.DeduplicationKey);
    }

    [Fact]
    public async Task InvokeAsync_AllowsDifferentEvents_ForSameEndpoint() {
        FakeBloomFilter filter = new("test-dedup");
        BloomFilterDeduplicationOptions options = new() {
            KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
        };
        BloomFilterDeduplicationMiddleware middleware = new(filter, options, NullLogger<BloomFilterDeduplicationMiddleware>.Instance);

        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

        WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\": 100}");

        WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\": 200}");

        int downstreamCallCount = 0;
        WebhookDelegate next = (ctx, ct) => {
            downstreamCallCount++;
            ctx.SetResult(WebhookDeliveryResult.Success(200, "OK"));
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context1, next);
        await middleware.InvokeAsync(context2, next);

        Assert.Equal(2, downstreamCallCount);
    }

    [Fact]
    public void Options_Validate_Throws_OnInvalidValues() {
        BloomFilterDeduplicationOptions options = new() {
            Capacity = 0
        };
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.Capacity = 1000;
        options.ErrorRate = 0.0;
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.ErrorRate = 1.0;
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.ErrorRate = 0.01;
        options.KeySelector = null!;
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void UseBloomFilterDeduplication_RegistersMiddlewareInContainer() {
        ServiceCollection services = new();
        FakeBloomFilter filter = new("test-filter");

        services.AddLogging();
        services.AddWiaojWebhooks(options => {
            options.UseBloomFilterDeduplication(filter);
        });

        ServiceProvider sp = services.BuildServiceProvider();
        BloomFilterDeduplicationMiddleware middleware = sp.GetRequiredService<BloomFilterDeduplicationMiddleware>();
        Assert.NotNull(middleware);
    }

    [Fact]
    public void UseBloomFilterDeduplication_WithKeyedFilterName_ResolvesCorrectly() {
        ServiceCollection services = new();
        FakeBloomFilter filter = new("test-dedup");

        services.AddLogging();
        services.AddKeyedSingleton<IBloomFilter>("test-dedup", filter);
        services.AddWiaojWebhooks(options => {
            options.UseBloomFilterDeduplication("test-dedup");
        });

        ServiceProvider sp = services.BuildServiceProvider();
        BloomFilterDeduplicationMiddleware middleware = sp.GetRequiredService<BloomFilterDeduplicationMiddleware>();
        Assert.NotNull(middleware);
    }
}