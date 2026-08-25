using Wiaoj.Webhooks.Concurrency;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Concurrency;

[Trait("Category", "Unit")]
[Trait("Feature", "Concurrency")]
[Trait("Component", "MailboxLock")]
public sealed class EndpointMailboxDeliveryLockTests {
    private readonly EndpointMailboxDeliveryLock _lock = new();

    [Fact]
    public async Task AcquireLockAsync_SerializesConcurrentExecutions_ForSameEndpoint() {
        // Arrange
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("endpoint-serial");
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        Lock gate = new();

        // Act: 10 concurrent acquisitions on the same endpoint
        Task[] tasks = Enumerable.Range(0, 10).Select(async _ => {
            using(await this._lock.AcquireLockAsync(endpointId)) {
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

        // Assert: Maximum concurrent execution must be strictly 1
        Assert.Equal(1, maxObservedConcurrency);
    }

    [Fact]
    public async Task AcquireLockAsync_ExecutesInParallel_ForDifferentEndpoints() {
        // Arrange: 5 distinct endpoints
        int currentConcurrency = 0;
        int maxObservedConcurrency = 0;
        Lock gate = new();

        // Act
        Task[] tasks = Enumerable.Range(0, 5).Select(async i => {
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId($"endpoint-parallel-{i}");
            using(await this._lock.AcquireLockAsync(endpointId)) {
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

        // Assert: Distinct endpoints must not block each other
        Assert.True(maxObservedConcurrency > 1, $"Expected parallel execution (>1), but observed was {maxObservedConcurrency}");
    }

    [Fact]
    public async Task AcquireLockAsync_SafelyEvictsIdleNodes_AndAllowsReacquisition() {
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("endpoint-evict");

        // 1st acquisition and release (evicts node when RefCount reaches 0)
        using(IDisposable handle1 = await this._lock.AcquireLockAsync(endpointId)) {
            Assert.NotNull(handle1);
        }

        // 2nd acquisition creates a fresh node without ObjectDisposedException
        using IDisposable handle2 = await this._lock.AcquireLockAsync(endpointId);
        Assert.NotNull(handle2);
    }

    [Fact]
    public async Task AcquireLockAsync_RespectsCancellationToken() {
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("endpoint-cancel");
        using CancellationTokenSource cts = new();

        // Hold the lock with thread 1
        IDisposable handle = await this._lock.AcquireLockAsync(endpointId);

        // Thread 2 tries to acquire but gets cancelled
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => {
            await this._lock.AcquireLockAsync(endpointId, cts.Token);
        });

        handle.Dispose();
    }
}