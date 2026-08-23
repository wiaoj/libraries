using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Concurrency;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Concurrency;

[Trait("Category", "Unit")]
[Trait("Feature", "Concurrency")]
[Trait("Component", "PartitionedDelivery")]
public sealed class PartitionedDeliveryMiddlewareTests {

    public sealed class TheConstructor {
        [Fact]
        public void Constructor_Throws_WhenDependenciesAreNull() {
            StripedWebhookDeliveryLock deliveryLock = new(64);
            PartitionedDeliveryOptions options = new();

            Assert.ThrowsAny<ArgumentException>(() =>
                new PartitionedDeliveryMiddleware(null!, options, NullLogger<PartitionedDeliveryMiddleware>.Instance));

            Assert.ThrowsAny<ArgumentException>(() =>
                new PartitionedDeliveryMiddleware(deliveryLock, null!, NullLogger<PartitionedDeliveryMiddleware>.Instance));

            Assert.ThrowsAny<ArgumentException>(() =>
                new PartitionedDeliveryMiddleware(deliveryLock, options, null!));
        }
    }

    public sealed class TheInvokeAsyncMethod {
        [Fact]
        public async Task InvokeAsync_SerializesDeliveries_SharingSamePartitionKey() {
            EndpointMailboxDeliveryLock deliveryLock = new();
            PartitionedDeliveryOptions options = new();
            PartitionedDeliveryMiddleware middleware = new(deliveryLock, options, NullLogger<PartitionedDeliveryMiddleware>.Instance);

            const string sharedPartitionKey = "order-group-99";
            int concurrentExecutions = 0;
            int maxObservedConcurrency = 0;
            Lock gate = new();

            WebhookDelegate downstream = async (ctx, ct) => {
                lock(gate) {
                    concurrentExecutions++;
                    if(concurrentExecutions > maxObservedConcurrency) {
                        maxObservedConcurrency = concurrentExecutions;
                    }
                }

                await Task.Delay(25, ct);

                lock(gate) {
                    concurrentExecutions--;
                }
            };

            // Act: 8 contexts having the exact same PartitionKey
            Task[] tasks = Enumerable.Range(0, 8).Select(_ => {
                WebhookDeliveryContext context = WebhookTestFactory.CreateContext(partitionKey: sharedPartitionKey);
                return middleware.InvokeAsync(context, downstream, CancellationToken.None);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Must be strictly serialized (FIFO)
            Assert.Equal(1, maxObservedConcurrency);
        }

        [Fact]
        public async Task InvokeAsync_RespectsCustomDomainPartitionKeySelector() {
            EndpointMailboxDeliveryLock deliveryLock = new();
            PartitionedDeliveryOptions options = new() {
                PartitionKeySelector = ctx => ctx.Items.TryGetValue("domain_key", out object? key) && key is string str
                    ? str
                    : ctx.PartitionKey.Value
            };

            PartitionedDeliveryMiddleware middleware = new(deliveryLock, options, NullLogger<PartitionedDeliveryMiddleware>.Instance);

            int concurrentExecutions = 0;
            int maxObservedConcurrency = 0;
            Lock gate = new();

            WebhookDelegate downstream = async (ctx, ct) => {
                lock(gate) {
                    concurrentExecutions++;
                    if(concurrentExecutions > maxObservedConcurrency) {
                        maxObservedConcurrency = concurrentExecutions;
                    }
                }

                await Task.Delay(25, ct);

                lock(gate) {
                    concurrentExecutions--;
                }
            };

            WebhookDeliveryContext ctx1 = WebhookTestFactory.CreateContext(WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId("ep-1")));
            ctx1.Items["domain_key"] = "customer-aggregate-A";

            WebhookDeliveryContext ctx2 = WebhookTestFactory.CreateContext(WebhookTestFactory.CreateEndpoint(WebhookTestFactory.CreateEndpointId("ep-2")));
            ctx2.Items["domain_key"] = "customer-aggregate-A";

            Task task1 = middleware.InvokeAsync(ctx1, downstream, CancellationToken.None);
            Task task2 = middleware.InvokeAsync(ctx2, downstream, CancellationToken.None);

            await Task.WhenAll(task1, task2);

            Assert.Equal(1, maxObservedConcurrency);
        }

        [Fact]
        public async Task InvokeAsync_Throws_WhenContextOrNextIsNull() {
            EndpointMailboxDeliveryLock deliveryLock = new();
            PartitionedDeliveryMiddleware middleware = new(deliveryLock, new PartitionedDeliveryOptions(), NullLogger<PartitionedDeliveryMiddleware>.Instance);

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(null!, (ctx, ct) => Task.CompletedTask, CancellationToken.None));

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(context, null!, CancellationToken.None));
        }
    }
}