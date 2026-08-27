using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Idempotency;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Idempotency;

/// <summary>
/// Reproduces the concurrent-duplicate-delivery race window in <see cref="IdempotencyMiddleware"/>.
/// </summary>
/// <remarks>
/// <see cref="IdempotencyMiddleware"/> currently checks <c>IIdempotencyStore.ContainsAsync</c> and,
/// only after the downstream pipeline completes, commits the key via <c>MarkProcessedAsync</c>.
/// This is a classic check-then-act race: two concurrent deliveries carrying the same idempotency
/// key (e.g. an original delivery and a sender-side retry that overlaps it) can both observe
/// "not yet processed" and both reach the downstream pipeline, even though
/// <see cref="Wiaoj.Webhooks.IIdempotencyStore.TryMarkProcessedAsync"/> already exists on the store
/// and would close this window atomically (as used correctly by the inbound
/// <c>WebhookHubEndpointFilter</c>).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Feature", "Idempotency")]
[Trait("Component", "Middleware")]
public sealed class IdempotencyMiddlewareConcurrencyTests {

    public sealed class TheInvokeAsyncMethod {
        [Fact]
        public async Task InvokeAsync_ConcurrentDuplicateDeliveries_ShouldNotInvokeDownstreamMoreThanOnce() {
            // Arrange
            InMemoryIdempotencyStore store = new();
            DefaultIdempotencyKeyGenerator keyGenerator = new();
            IdempotencyOptions options = new() { Window = TimeSpan.FromMinutes(30) };
            IdempotencyMiddleware middleware = new(store, keyGenerator, options, NullLogger<IdempotencyMiddleware>.Instance);

            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint();
            int downstreamCalls = 0;

            // Simulate realistic delivery latency (e.g. an outbound HTTP call) so that a second,
            // near-simultaneous duplicate delivery has a real chance to observe the key as
            // "not yet processed" before the first delivery commits it.
            WebhookDelegate next = async (ctx, ct) => {
                Interlocked.Increment(ref downstreamCalls);
                await Task.Delay(150, ct);
                ctx.SetResult(WebhookTestFactory.CreateSuccessResult());
            };

            WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext(endpoint);
            WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext(endpoint);

            // Act: fire two deliveries for the same logical event concurrently, as would happen
            // if a sender retries before the first attempt has finished processing.
            Task delivery1 = middleware.InvokeAsync(context1, next, TestContext.Current.CancellationToken);
            Task delivery2 = middleware.InvokeAsync(context2, next, TestContext.Current.CancellationToken);
            await Task.WhenAll(delivery1, delivery2);

            // Assert: exactly one delivery should ever reach the downstream pipeline.
            // KNOWN BUG: currently fails with downstreamCalls == 2 — ContainsAsync + MarkProcessedAsync
            // is not atomic, so both concurrent deliveries pass the deduplication check.
            Assert.Equal(1, downstreamCalls);
        }
    }
}