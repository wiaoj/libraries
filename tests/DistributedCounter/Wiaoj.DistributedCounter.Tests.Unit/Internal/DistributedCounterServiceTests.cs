using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Service")]
[Trait("Feature", "BatchOperations")]
public sealed class DistributedCounterServiceTests {

    public sealed class TheBatchGetValues {

        [Fact]
        public async Task GivenEmptyNames_ReturnsEmptyCollectionImmediatelyWithoutStorageCall() {
            // Arrange
            DistributedCounterTestContext context = new();
            IDistributedCounterService service = context.CreateService();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterValueCollection result = await service.GetValuesAsync([], ct);

            // Assert within active scope
            Assert.Equal(0, result.Count);
            Assert.Equal(0, context.Storage.GetCallCount);
        }

        [Fact]
        public async Task GivenMultipleCounterNames_FetchesAllValuesInSingleBatch() {
            // Arrange
            DistributedCounterTestContext context = new(opt => opt.GlobalKeyPrefix = "app:");

            // Pre-seed storage
            context.Storage.SetupGetValue(new CounterKey("app:orders"), new CounterValue(100));
            context.Storage.SetupGetValue(new CounterKey("app:users"), new CounterValue(50));
            context.Storage.SetupGetValue(new CounterKey("app:errors"), new CounterValue(2));

            IDistributedCounterService service = context.CreateService();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterValueCollection result = await service.GetValuesAsync(["orders", "users", "errors", "missing"], ct);

            // Assert
            Assert.Equal(4, result.Count);
            Assert.Equal(100, result["orders"].Value);
            Assert.Equal(50, result["users"].Value);
            Assert.Equal(2, result["errors"].Value);
            Assert.Equal(0, result["missing"].Value);
        }
    }

    public sealed class TheRaceConditionsAndHighConcurrency {

        [Fact]
        public async Task ConcurrentBatchQueries_UnderHighPoolContention_DoNotCorruptStateOrLeak() {
            // Arrange
            DistributedCounterTestContext context = new(opt => opt.GlobalKeyPrefix = "app:");

            for(int i = 0; i < 20; i++) {
                context.Storage.SetupGetValue(new CounterKey($"app:metric_{i}"), new CounterValue(i * 10));
            }

            IDistributedCounterService service = context.CreateService();
            CancellationToken ct = TestContext.Current.CancellationToken;

            const int concurrency = 50;
            const int queriesPerTask = 20;

            // Act: 50 tasks hammering the service and pool concurrently (1000 total batch queries)
            Task[] tasks = [.. Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(async () => {
                    for (int q = 0; q < queriesPerTask; q++) {
                        string[] keys = ["metric_1", "metric_5", "metric_10", "metric_15"];
                        CounterValueCollection values = await service.GetValuesAsync(keys, ct);

                        // Strict integrity assertion during high concurrency
                        Assert.Equal(10, values["metric_1"].Value);
                        Assert.Equal(50, values["metric_5"].Value);
                        Assert.Equal(100, values["metric_10"].Value);
                        Assert.Equal(150, values["metric_15"].Value);
                    }
                }, ct))];

            await Task.WhenAll(tasks);
        }

        [Fact]
        public async Task ConcurrentIncrements_WhileFlushingAll_LoseZeroData() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.GlobalKeyPrefix = "app:";
                opt.DefaultStrategy = CounterStrategy.Buffered;
            });

            IDistributedCounterFactory factory = context.CreateFactory();
            IDistributedCounterService service = context.CreateService();
            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter counter = factory.Create("high_traffic_counter");

            const int concurrency = 30;
            const int incrementsPerTask = 100;
            const int expectedTotal = concurrency * incrementsPerTask;

            // Act: Fire parallel increments while concurrently triggering FlushAllAsync
            Task[] incrementTasks = [.. Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(async () => {
                    for (int i = 0; i < incrementsPerTask; i++) {
                        await counter.IncrementAsync(1, CounterExpiry.Infinite, ct);
                    }
                }, ct))];

            Task flushTask = Task.Run(async () => {
                for(int f = 0; f < 5; f++) {
                    await service.FlushAllAsync(ct);
                    await Task.Yield();
                }
            }, ct);

            await Task.WhenAll(incrementTasks.Concat([flushTask]));

            // Final flush to guarantee remaining deltas in RAM are sent to storage
            await service.FlushAllAsync(ct);

            // Assert: Total in storage MUST equal expectedTotal exactly! (Zero lost increments)
            CounterValue finalVal = await counter.GetValueAsync(ct);
            Assert.Equal(expectedTotal, finalVal.Value);
        }
    }

    public sealed class TheFlushAndResetOperations {

        [Fact]
        public async Task ResetAllAsync_ClearsAllTrackedCountersAcrossSystem() {
            // Arrange
            DistributedCounterTestContext context = new(opt => opt.DefaultStrategy = CounterStrategy.Buffered);
            IDistributedCounterFactory factory = context.CreateFactory();
            IDistributedCounterService service = context.CreateService();
            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter c1 = factory.Create("c1");
            IDistributedCounter c2 = factory.Create("c2");

            await c1.IncrementAsync(50, CounterExpiry.Infinite, ct);
            await c2.IncrementAsync(80, CounterExpiry.Infinite, ct);

            // Act
            await service.ResetAllAsync(ct);

            // Assert: Local counters reset to 0, and factory cache is purged
            Assert.Equal(0, (await c1.GetValueAsync(ct)).Value);
            Assert.Equal(0, (await c2.GetValueAsync(ct)).Value);
            Assert.Empty(((IBufferedCounterSource)factory).GetAllTrackedCounters());
        }
    }
}