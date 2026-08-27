using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Extensions;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.AspNetCore;
using Wiaoj.Webhooks.AspNetCore.Authentication;
using Wiaoj.Webhooks.AspNetCore.Filters;
using Wiaoj.Webhooks.AspNetCore.Metadata;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Signing;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.AspNetCore;

[Trait("Category", "Unit")]
[Trait("Feature", "InboundReceiver")]
[Trait("Component", "ConcurrencyRace")]
public sealed class InboundConcurrencyRaceTests {
    private const string SecretKey = "whsec_race_test_secret_123456789";
    private readonly HmacSha256WebhookSigner _signer = new();

    private static ServiceProvider BuildServiceProvider(TimeProvider? timeProvider = null) {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton(timeProvider ?? TimeProvider.System);
        services.AddSingleton<IWebhookEventRegistry>(new WebhookEventRegistry(new WebhookEventRegistryOptions((typeof(OrderCreatedWebhookEvent), "order.created"))));
        services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddOptions<WebhookInboundOptions>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InvokeAsync_WhenIdenticalRequestsArriveSimultaneously_ExecutesHandlerExactlyOnce() {
        // Arrange: Deterministic time provider with 10 concurrent requests
        FakeTimeProvider timeProvider = new();
        DateTimeOffset fixedNow = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(fixedNow);

        using ServiceProvider sp = BuildServiceProvider(timeProvider);

        const string body = "{\"OrderId\":\"ORD-RACE-CONCURRENCY\",\"Amount\":10.0}";
        UnixTimestamp timestamp = UnixTimestamp.FromSeconds(fixedNow.ToUnixTimeSeconds());
        WebhookSignature sig = this._signer.Sign(body.ToUtf8Bytes(), SecretKey.ToUtf8Bytes(), timestamp);

        int handlerExecutionCount = 0;
        Delegate slowHandler = async () => {
            Interlocked.Increment(ref handlerExecutionCount);
            await Task.Delay(100);
            return Results.Ok();
        };

        WebhookReceiverEndpointMetadata metadata = new() {
            SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
            EnforceIdempotency = true,
            Tolerance = TimeSpan.FromMinutes(5)
        };
        WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, slowHandler);

        // Act: Fire 10 concurrent requests targeting the same payload
        Task[] tasks = [.. Enumerable.Range(0, 10).Select(async _ => {
            DefaultHttpContext ctx = new() { RequestServices = sp };
            ctx.Request.Path = "/api/webhooks/orders";
            ctx.Request.Method = "POST";
            ctx.Request.Headers[WebhookHeaderNames.WebhookSignature] = sig.HeaderValue;
            ctx.Request.Body = new MemoryStream(body.ToUtf8Bytes());
            ctx.Request.ContentLength = body.AsSpan().GetUtf8ByteCount();

            EndpointFilterInvocationContext invCtx = new DefaultEndpointFilterInvocationContext(ctx);
            await filter.InvokeAsync(invCtx, static _ => ValueTask.FromResult<object?>(Results.Ok()));
        })];

        await Task.WhenAll(tasks);

        // Assert: Atomic claim ensures exactly one execution
        Assert.Equal(1, handlerExecutionCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenDistinctEventsArriveSimultaneously_ExecutesHandlerForAllEvents() {
        // Arrange
        FakeTimeProvider timeProvider = new();
        DateTimeOffset fixedNow = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(fixedNow);

        using ServiceProvider sp = BuildServiceProvider(timeProvider);

        int executedOrdersCount = 0;
        Delegate handler = () => {
            Interlocked.Increment(ref executedOrdersCount);
            return Results.Ok();
        };

        WebhookReceiverEndpointMetadata metadata = new() {
            SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
            EnforceIdempotency = true,
            Tolerance = TimeSpan.FromMinutes(5)
        };
        WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, handler);

        // Act: Fire 5 distinct order payloads in parallel
        Task[] tasks = [.. Enumerable.Range(1, 5).Select(async i => {
            string body = $"{{\"OrderId\":\"ORD-DISTINCT-{i}\",\"Amount\":{i * 10}.0}}";
            UnixTimestamp timestamp = UnixTimestamp.FromSeconds(fixedNow.ToUnixTimeSeconds());
            WebhookSignature sig = this._signer.Sign(body.ToUtf8Bytes(), SecretKey.ToUtf8Bytes(), timestamp);

            DefaultHttpContext ctx = new() { RequestServices = sp };
            ctx.Request.Path = "/api/webhooks/orders";
            ctx.Request.Method = "POST";
            ctx.Request.Headers[WebhookHeaderNames.WebhookSignature] = sig.HeaderValue;
            ctx.Request.Body = new MemoryStream(body.ToUtf8Bytes());
            ctx.Request.ContentLength = body.AsSpan().GetUtf8ByteCount();

            EndpointFilterInvocationContext invCtx = new DefaultEndpointFilterInvocationContext(ctx);
            await filter.InvokeAsync(invCtx, static _ => ValueTask.FromResult<object?>(Results.Ok()));
        })];

        await Task.WhenAll(tasks);

        // Assert: All 5 distinct payloads must be executed
        Assert.Equal(5, executedOrdersCount);
    }

    [Fact]
    public async Task InvokeAsync_WhenHandlerThrows_RollsBackIdempotencyClaim_AllowingSubsequentRetry() {
        // Arrange: Fixed time with FakeTimeProvider
        FakeTimeProvider timeProvider = new();
        DateTimeOffset fixedNow = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(fixedNow);

        using ServiceProvider sp = BuildServiceProvider(timeProvider);

        const string body = "{\"OrderId\":\"ORD-ROLLBACK-TEST\",\"Amount\":49.90}";
        UnixTimestamp timestamp = UnixTimestamp.FromSeconds(fixedNow.ToUnixTimeSeconds());
        WebhookSignature sig = this._signer.Sign(body.ToUtf8Bytes(), SecretKey.ToUtf8Bytes(), timestamp);

        int executionAttempts = 0;
        Delegate failingThenSucceedingHandler = () => {
            int attempt = Interlocked.Increment(ref executionAttempts);
            if(attempt == 1) {
                throw new InvalidOperationException("Simulated transient database failure");
            }
            return Results.Ok();
        };

        WebhookReceiverEndpointMetadata metadata = new() {
            SecretResolver = new SecretWebhookSecretResolver(Secret.From(SecretKey)),
            EnforceIdempotency = true,
            Tolerance = TimeSpan.FromMinutes(5)
        };
        WebhookReceiverEndpointFilter<OrderCreatedWebhookEvent> filter = new(metadata, failingThenSucceedingHandler);

        DefaultHttpContext CreateContext() {
            DefaultHttpContext ctx = new() { RequestServices = sp };
            ctx.Request.Path = "/api/webhooks/orders";
            ctx.Request.Method = "POST";
            ctx.Request.Headers[WebhookHeaderNames.WebhookSignature] = sig.HeaderValue;
            ctx.Request.Body = new MemoryStream(body.ToUtf8Bytes());
            ctx.Request.ContentLength = body.AsSpan().GetUtf8ByteCount();
            return ctx;
        }

        // Act 1: First attempt fails and throws the unhandled exception
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(CreateContext()), static _ => ValueTask.FromResult<object?>(Results.Ok())).AsTask());

        // Act 2: Upstream retries the exact same webhook payload
        object? secondResult = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(CreateContext()), static _ => ValueTask.FromResult<object?>(Results.Ok()));

        // Assert: Rollback allowed the second attempt to execute and succeed
        Assert.Equal(2, executionAttempts);
        IStatusCodeHttpResult statusResult = Assert.IsType<IStatusCodeHttpResult>(secondResult, exactMatch: false);
        Assert.Equal(StatusCodes.Status200OK, statusResult.StatusCode);
    }
}