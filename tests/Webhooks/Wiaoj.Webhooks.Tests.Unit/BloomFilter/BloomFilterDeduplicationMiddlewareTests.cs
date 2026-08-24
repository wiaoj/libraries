using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Webhooks.BloomFilter;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.BloomFilter;

[Trait("Category", "Unit")]
[Trait("Feature", "Deduplication")]
[Trait("Component", "BloomFilter")]
public sealed class BloomFilterDeduplicationMiddlewareTests {
    private readonly FakeBloomFilter _filter = new("test-dedup");
    private readonly BloomFilterDeduplicationOptions _options = new();

    private BloomFilterDeduplicationMiddleware CreateMiddleware(BloomFilterDeduplicationOptions? options = null) {
        return new(this._filter, options ?? this._options, NullLogger<BloomFilterDeduplicationMiddleware>.Instance);
    }

    // ── 1. GUARD CLAUSE & PRECONDITION TESTS ──────────────────────────────────

    [Fact]
    public void Constructor_Throws_WhenParametersAreNull() {
        // PrecaArgumentNullException, ArgumentException hiyerarşisindedir:
        Assert.ThrowsAny<ArgumentException>(() =>
            new BloomFilterDeduplicationMiddleware(null!, this._options, NullLogger<BloomFilterDeduplicationMiddleware>.Instance));

        Assert.ThrowsAny<ArgumentException>(() =>
            new BloomFilterDeduplicationMiddleware(this._filter, null!, NullLogger<BloomFilterDeduplicationMiddleware>.Instance));

        Assert.ThrowsAny<ArgumentException>(() =>
            new BloomFilterDeduplicationMiddleware(this._filter, this._options, null!));
    }

    [Fact]
    public async Task InvokeAsync_Throws_WhenContextOrNextIsNull() {
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDelegate next = (ctx, ct) => Task.CompletedTask;

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            middleware.InvokeAsync(null!, next, CancellationToken.None));

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            middleware.InvokeAsync(context, null!, CancellationToken.None));
    }

    // ── 2. DEDUPLICATION & SUCCESS FLOWS ─────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_PassesFirstEvent_AndBlocksDuplicateAfterSuccessfulDelivery() {
        FakeBloomFilter filter = new("test-dedup");
        BloomFilterDeduplicationOptions options = new();
        BloomFilterDeduplicationMiddleware middleware = new(filter, options, NullLogger<BloomFilterDeduplicationMiddleware>.Instance);

        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        IWebhookEvent @event = WebhookTestFactory.CreateEvent();

        int downstreamCallCount = 0;
        WebhookDelegate next = (ctx, ct) => {
            downstreamCallCount++;
            ctx.SetResult(WebhookDeliveryResult.Success(200, "OK"));
            return Task.CompletedTask;
        };

        // ── 1. İstek (İlk Gönderim -> Başarılı Olmalı) ──
        WebhookDeliveryContext firstContext = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-100\"}");

        await middleware.InvokeAsync(firstContext, next, CancellationToken.None);
        Assert.Equal(1, downstreamCallCount);

        // ── 2. İstek (Mükerrer Gönderim -> Downstream Çağrılmadan Engellenmeli) ──
        WebhookDeliveryContext duplicateContext = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-100\"}");

        await middleware.InvokeAsync(duplicateContext, next, CancellationToken.None);

        Assert.Equal(1, downstreamCallCount); // Downstream tekrar çağrılmamalı

        Assert.True(duplicateContext.TryGetResult(out WebhookDeliveryResult? result));
        WebhookDeliveryResult.Deduplicated dedup = Assert.IsType<WebhookDeliveryResult.Deduplicated>(result);
        Assert.True(dedup.IsSuccess);
        Assert.Equal(options.KeySelector(duplicateContext), dedup.DeduplicationKey);
    }

    [Fact]
    public async Task InvokeAsync_AllowsDifferentPayloads_ForSameEndpoint() {
        // Payload bazlı key selector kullanıyoruz ki payload farklılığı ayırt edilsin:
        BloomFilterDeduplicationOptions options = new() {
            KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
        };
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware(options);
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();

        WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-1\"}");

        WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-2\"}");

        int delivererInvocationCount = 0;
        WebhookDelegate successfulDeliverer = (ctx, ct) => {
            delivererInvocationCount++;
            ctx.SetResult(WebhookTestFactory.CreateSuccessResult());
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context1, successfulDeliverer, CancellationToken.None);
        await middleware.InvokeAsync(context2, successfulDeliverer, CancellationToken.None);

        Assert.Equal(2, delivererInvocationCount);
    }

    // ── 3. RETRY & FAILURE OUTCOME-BASED FLOWS ────────────────────────────────

    [Fact]
    public async Task InvokeAsync_WhenPreviousAttemptFailed_ShouldNotSkipRetryAttempt() {
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware();
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        IWebhookEvent @event = WebhookTestFactory.CreateEvent();
        int delivererInvocationCount = 0;

        WebhookDelegate failingDeliverer = (ctx, ct) => {
            delivererInvocationCount++;
            ctx.SetResult(WebhookTestFactory.CreateFailureResult("503 Service Unavailable", 503));
            return Task.CompletedTask;
        };

        // 1. Deneme (Başarısız)
        WebhookDeliveryContext firstAttempt = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-FAIL\"}");

        await middleware.InvokeAsync(firstAttempt, failingDeliverer, CancellationToken.None);
        Assert.Equal(1, delivererInvocationCount);

        // 2. Deneme (Retry Attempt)
        WebhookDeliveryAttempt failedHistory = WebhookTestFactory.CreateAttempt(
            WebhookTestFactory.CreateFailureResult("503 Service Unavailable", 503));

        WebhookDeliveryContext retryAttempt = WebhookTestFactory.CreateContext(
            endpoint: endpoint,
            serializedPayload: "{\"orderId\":\"ORD-FAIL\"}",
            attemptHistory: [failedHistory]);

        await middleware.InvokeAsync(retryAttempt, failingDeliverer, CancellationToken.None);

        Assert.Equal(2, delivererInvocationCount); // Retry başarıyla teslimata gitmeli
    }

    [Fact]
    public async Task InvokeAsync_WhenDeliveryFails_ShouldNotAddKeyToBloomFilter() {
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        string key = this._options.KeySelector(context);

        WebhookDelegate failingDeliverer = (ctx, ct) => {
            ctx.SetResult(WebhookTestFactory.CreateFailureResult("Gateway Timeout", 504));
            return Task.CompletedTask;
        };

        await middleware.InvokeAsync(context, failingDeliverer, CancellationToken.None);

        Assert.False(this._filter.Contains(key.AsSpan()));
    }

    [Fact]
    public async Task InvokeAsync_WhenDeliveryThrows_ShouldNotAddKeyToBloomFilter() {
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        string key = this._options.KeySelector(context);

        WebhookDelegate throwingDeliverer = (ctx, ct) => throw new HttpRequestException("Network unreachable");

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            middleware.InvokeAsync(context, throwingDeliverer, CancellationToken.None));

        Assert.False(this._filter.Contains(key.AsSpan()));
    }

    // ── 4. CONCURRENCY & CANCELLATION ────────────────────────────────────────

    [Fact]
    public async Task InvokeAsync_UnderConcurrentAttempts_OnlyDeliversDistinctEvents() {
        BloomFilterDeduplicationOptions options = new() {
            KeySelector = ctx => $"{ctx.Endpoint.Id.Value}:{ctx.SerializedPayload}"
        };
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware(options);
        WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
        int successfulDeliveries = 0;

        WebhookDelegate deliverer = (ctx, ct) => {
            Interlocked.Increment(ref successfulDeliveries);
            ctx.SetResult(WebhookTestFactory.CreateSuccessResult());
            return Task.CompletedTask;
        };

        // 10 farklı event'i paralel olarak 5'er kez gönderiyoruz
        Task[] tasks = [.. Enumerable.Range(0, 50).Select(i => {
            int eventIndex = i % 10;
            WebhookDeliveryContext ctx = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                serializedPayload: $"{{\"orderId\":\"ORD-{eventIndex}\"}}");
            return middleware.InvokeAsync(ctx, deliverer);
        })];

        await Task.WhenAll(tasks);

        Assert.True(successfulDeliveries <= 10);
    }

    [Fact]
    public async Task InvokeAsync_RespectsCancellationToken() {
        BloomFilterDeduplicationMiddleware middleware = CreateMiddleware();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        using CancellationTokenSource cts = new();
        cts.Cancel();

        WebhookDelegate deliverer = (ctx, ct) => {
            ct.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            middleware.InvokeAsync(context, deliverer, cts.Token));
    }
}