using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Concurrency;

[Trait("Category", "Unit")]
[Trait("Feature", "Concurrency")]
[Trait("Component", "PartitionedDelivery")]
public sealed class PartitionedDeliveryMiddlewareTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR GUARD
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenDeliveryLockIsNull() {
            Assert.ThrowsAny<ArgumentException>(() =>
                new PartitionedDeliveryMiddleware(null!));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. CONCURRENCY & SERIALIZATION BEHAVIOR
    // ────────────────────────────────────────────────────────────────────────

    public sealed class WhenDeliveringConcurrently {
        [Fact]
        public async Task InvokeAsync_SerializesDeliveries_ForSameEndpoint() {
            // Arrange: In-memory lock with 4096 stripes
            StripedWebhookDeliveryLock deliveryLock = new(4096);
            PartitionedDeliveryMiddleware middleware = new(deliveryLock);

            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("same-endpoint");
            WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(endpointId);

            int concurrentExecutionCount = 0;
            int maxObservedConcurrency = 0;
            Lock syncLock = new();

            WebhookDelegate next = async (ctx, ct) => {
                lock(syncLock) {
                    concurrentExecutionCount++;
                    if(concurrentExecutionCount > maxObservedConcurrency) {
                        maxObservedConcurrency = concurrentExecutionCount;
                    }
                }

                // Simulate processing latency to test concurrency
                await Task.Delay(20, ct);

                lock(syncLock) {
                    concurrentExecutionCount--;
                }
            };

            // Act: Dispatch 10 concurrent requests to the SAME endpoint
            Task[] tasks = Enumerable.Range(0, 10).Select(_ => {
                WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);
                return middleware.InvokeAsync(context, next, CancellationToken.None);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Execution count at any given moment must NEVER exceed 1 (strictly sequential)
            Assert.Equal(1, maxObservedConcurrency);
        }

        [Fact]
        public async Task InvokeAsync_AllowsParallelExecution_ForDifferentEndpoints() {
            // Arrange: In-memory lock with 4096 stripes
            StripedWebhookDeliveryLock deliveryLock = new(4096);
            PartitionedDeliveryMiddleware middleware = new(deliveryLock);

            int concurrentExecutionCount = 0;
            int maxObservedConcurrency = 0;
            Lock syncLock = new();

            WebhookDelegate next = async (ctx, ct) => {
                lock(syncLock) {
                    concurrentExecutionCount++;
                    if(concurrentExecutionCount > maxObservedConcurrency) {
                        maxObservedConcurrency = concurrentExecutionCount;
                    }
                }

                // Simulate processing latency to observe parallel execution
                await Task.Delay(50, ct);

                lock(syncLock) {
                    concurrentExecutionCount--;
                }
            };

            // Act: Dispatch concurrent requests across 5 DIFFERENT endpoints
            Task[] tasks = Enumerable.Range(0, 5).Select(i => {
                WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId($"endpoint-{i}");
                WebhookEndpoint endpoint = WebhookTestFactory.CreateEndpoint(endpointId);
                WebhookDeliveryContext context = WebhookTestFactory.CreateContext(endpoint);

                return middleware.InvokeAsync(context, next, CancellationToken.None);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Different endpoints must not block each other; concurrency should be greater than 1
            Assert.True(maxObservedConcurrency > 1, $"Expected parallel execution (>1), but observed max concurrency was {maxObservedConcurrency}");
        }
    }
}