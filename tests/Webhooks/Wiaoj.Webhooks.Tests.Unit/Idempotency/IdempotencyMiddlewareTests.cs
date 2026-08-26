using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Idempotency;

[Trait("Category", "Unit")]
[Trait("Feature", "Idempotency")]
[Trait("Component", "Middleware")]
public sealed class IdempotencyMiddlewareTests {

    public sealed class TheInvokeAsyncMethod {
        [Fact]
        public async Task InvokeAsync_PassesFirstEvent_AndShortCircuitsDuplicate() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryIdempotencyStore store = new(timeProvider);
            DefaultIdempotencyKeyGenerator keyGenerator = new();
            IdempotencyOptions options = new() { Window = TimeSpan.FromMinutes(30) };
            IdempotencyMiddleware middleware = new(store, keyGenerator, options, NullLogger<IdempotencyMiddleware>.Instance);

            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
            int downstreamCalls = 0;
            WebhookDelegate next = (ctx, ct) => {
                downstreamCalls++;
                ctx.SetResult(WebhookTestFactory.CreateSuccessResult());
                return Task.CompletedTask;
            };

            // 1st request -> Successfully passes through and gets committed
            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(endpoint);
            await middleware.InvokeAsync(context1, next, TestContext.Current.CancellationToken);
            Assert.Equal(1, downstreamCalls);

            // 2nd request -> Intercepted as duplicate without calling downstream
            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(endpoint);
            await middleware.InvokeAsync(context2, next, TestContext.Current.CancellationToken);

            Assert.Equal(1, downstreamCalls);
            Assert.True(context2.TryGetResult(out WebhookDeliveryResult? result));
            Assert.IsType<WebhookDeliveryResult.Deduplicated>(result);
        }

        [Fact]
        public async Task InvokeAsync_RespectsCustomKeySelector() {
            // Arrange
            InMemoryIdempotencyStore store = new();
            DefaultIdempotencyKeyGenerator keyGenerator = new();
            IdempotencyOptions options = new() {
                CustomKeySelector = ctx => new IdempotencyKey($"custom:{ctx.Endpoint.Id.Value}")
            };
            IdempotencyMiddleware middleware = new(store, keyGenerator, options, NullLogger<IdempotencyMiddleware>.Instance);

            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
            int downstreamCalls = 0;
            WebhookDelegate next = (ctx, ct) => {
                downstreamCalls++;
                ctx.SetResult(WebhookTestFactory.CreateSuccessResult());
                return Task.CompletedTask;
            };

            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(endpoint);
            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(endpoint);

            await middleware.InvokeAsync(context1, next, TestContext.Current.CancellationToken);
            await middleware.InvokeAsync(context2, next, TestContext.Current.CancellationToken);

            Assert.Equal(1, downstreamCalls);
            Assert.True(context2.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.Deduplicated dedup = Assert.IsType<WebhookDeliveryResult.Deduplicated>(result);
            Assert.Equal($"custom:{endpoint.Id.Value}", dedup.DeduplicationKey);
        }

        [Fact]
        public async Task InvokeAsync_WhenFirstAttemptFails_ShouldNotBlockSubsequentRetryAttempts() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryIdempotencyStore store = new(timeProvider);
            DefaultIdempotencyKeyGenerator keyGenerator = new();
            IdempotencyOptions options = new() { Window = TimeSpan.FromMinutes(30) };
            IdempotencyMiddleware middleware = new(store, keyGenerator, options, NullLogger<IdempotencyMiddleware>.Instance);

            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
            int downstreamDeliveryCount = 0;

            WebhookDelegate downstream = (ctx, ct) => {
                downstreamDeliveryCount++;
                if(ctx.IsFirstAttempt()) {
                    ctx.SetResult(WebhookTestFactory.CreateTransientFailureResult("503 Service Unavailable", 503));
                }
                else {
                    ctx.SetResult(WebhookTestFactory.CreateSuccessResult(200, "OK"));
                }
                return Task.CompletedTask;
            };

            // ── Attempt #1: Fails with transient 503 error ──
            WebhookDeliveryContext attempt1Context = WebhookTestFactory.CreateContext(endpoint);
            await middleware.InvokeAsync(attempt1Context, downstream, TestContext.Current.CancellationToken);

            Assert.Equal(1, downstreamDeliveryCount);
            Assert.True(attempt1Context.TryGetResult(out WebhookDeliveryResult? result1));
            Assert.IsType<WebhookDeliveryResult.TransientFailure>(result1);

            // ── Attempt #2 (Retry): Re-attempted delivery with prior history ──
            WebhookDeliveryAttempt failedAttempt = WebhookTestFactory.CreateAttempt(
                1,
                WebhookTestFactory.CreateTransientFailureResult("503 Service Unavailable", 503));

            WebhookDeliveryContext attempt2Context = WebhookTestFactory.CreateContext(
                endpoint: endpoint,
                attemptHistory: [failedAttempt]);

            // Act
            await middleware.InvokeAsync(attempt2Context, downstream, TestContext.Current.CancellationToken);

            // Assert: Retry must reach downstream deliverer and succeed
            Assert.Equal(2, downstreamDeliveryCount);
            Assert.True(attempt2Context.TryGetResult(out WebhookDeliveryResult? result2));
            Assert.IsType<WebhookDeliveryResult.Delivered>(result2);
        }
    }
}