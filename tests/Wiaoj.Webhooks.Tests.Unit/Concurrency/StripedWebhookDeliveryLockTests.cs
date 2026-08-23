using Wiaoj.Webhooks.Concurrency;

namespace Wiaoj.Webhooks.Tests.Unit.Concurrency;

[Trait("Category", "Unit")]
[Trait("Feature", "Concurrency")]
[Trait("Component", "StripedLock")]
public sealed class StripedWebhookDeliveryLockTests {

    public sealed class TheConstructor {
        [Theory]
        [InlineData(2)]
        [InlineData(16)]
        [InlineData(256)]
        [InlineData(4096)]
        public void Constructor_Succeeds_WhenStripeCountIsPowerOfTwo(int validStripes) {
            using StripedWebhookDeliveryLock deliveryLock = new(validStripes);
            Assert.NotNull(deliveryLock);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(3)]
        [InlineData(100)]
        [InlineData(1000)]
        public void Constructor_Throws_WhenStripeCountIsNotPositivePowerOfTwo(int invalidStripes) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new StripedWebhookDeliveryLock(invalidStripes));
        }
    }

    public sealed class TheAcquireLockAsyncMethod {
        [Fact]
        public async Task AcquireLockAsync_SerializesConcurrentAccess_ForIdenticalPartitionKey() {
            using StripedWebhookDeliveryLock deliveryLock = new(stripeCount: 16);
            const string partitionKey = "tenant-accounting-1";

            int currentConcurrency = 0;
            int maxObservedConcurrency = 0;
            Lock gate = new();

            // Act: 10 concurrent tasks competing for the exact same partition key
            Task[] tasks = Enumerable.Range(0, 10).Select(async _ => {
                using(await deliveryLock.AcquireLockAsync(partitionKey)) {
                    lock(gate) {
                        currentConcurrency++;
                        if(currentConcurrency > maxObservedConcurrency) {
                            maxObservedConcurrency = currentConcurrency;
                        }
                    }

                    await Task.Delay(20);

                    lock(gate) {
                        currentConcurrency--;
                    }
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Max concurrent execution on same partition must be strictly 1
            Assert.Equal(1, maxObservedConcurrency);
        }

        [Fact]
        public async Task AcquireLockAsync_AllowsParallelExecution_ForDifferentPartitionKeys() {
            // Use large stripe table (4096) to minimize accidental hash collision across 5 keys
            using StripedWebhookDeliveryLock deliveryLock = new(stripeCount: 4096);
            string[] partitionKeys = ["order-1", "order-2", "order-3", "order-4", "order-5"];

            int currentConcurrency = 0;
            int maxObservedConcurrency = 0;
            Lock gate = new();

            Task[] tasks = partitionKeys.Select(async key => {
                using(await deliveryLock.AcquireLockAsync(key)) {
                    lock(gate) {
                        currentConcurrency++;
                        if(currentConcurrency > maxObservedConcurrency) {
                            maxObservedConcurrency = currentConcurrency;
                        }
                    }

                    await Task.Delay(50);

                    lock(gate) {
                        currentConcurrency--;
                    }
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Different partition keys must execute concurrently in parallel
            Assert.True(maxObservedConcurrency > 1, $"Observed concurrency was {maxObservedConcurrency}, expected > 1");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task AcquireLockAsync_Throws_WhenPartitionKeyIsNullOrWhiteSpace(string? invalidKey) {
            using StripedWebhookDeliveryLock deliveryLock = new(16);

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                deliveryLock.AcquireLockAsync(invalidKey!).AsTask());
        }

        [Fact]
        public async Task AcquireLockAsync_RespectsCancellationToken() {
            using StripedWebhookDeliveryLock deliveryLock = new(16);
            const string partitionKey = "contested-key";

            using IDisposable initialHandle = await deliveryLock.AcquireLockAsync(partitionKey);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                deliveryLock.AcquireLockAsync(partitionKey, cts.Token).AsTask());
        }
    }
}