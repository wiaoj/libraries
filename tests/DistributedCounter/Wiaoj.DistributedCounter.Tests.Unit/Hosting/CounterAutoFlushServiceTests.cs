using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Hosting;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Hosting;

[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "AutoFlush")]
public sealed class CounterAutoFlushServiceTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenFactoryNotImplementingIBufferedCounterSource_ThrowsInvalidOperationException() {
            // Arrange
            IDistributedCounterFactory nonBufferedFactory = new NonBufferedDummyFactory();
            DistributedCounterOptions options = new();
            FakeTimeProvider timeProvider = new();

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => new CounterAutoFlushService(
                nonBufferedFactory,
                Options.Create(options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance));
        }

        private sealed class NonBufferedDummyFactory : IDistributedCounterFactory {
            public IDistributedCounter Create(string name) {
                throw new NotImplementedException();
            }

            public IDistributedCounter Create<TTag>() where TTag : notnull {
                throw new NotImplementedException();
            }

            public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull {
                throw new NotImplementedException();
            }

            public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull {
                throw new NotImplementedException();
            }
        }
    }

    public sealed class TheExecutionLoop {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeFlushInterval_DisablesWorkerImmediately(int intervalSeconds) {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.AutoFlushInterval = TimeSpan.FromSeconds(intervalSeconds);
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            using CancellationTokenSource cts = new();

            // Act
            Task executeTask = service.StartAsync(cts.Token);
            await executeTask;

            // Assert: Service finishes execution immediately without errors
            Assert.True(executeTask.IsCompleted);
        }

        [Fact]
        public async Task WhenTimerTicks_FlushesPendingDeltasToRemoteStorage() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AutoFlushInterval = TimeSpan.FromSeconds(5);
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter c1 = context.Factory.Create("metric_1");
            IDistributedCounter c2 = context.Factory.Create("metric_2");

            await c1.IncrementAsync(25, CounterExpiry.Infinite, ct);
            await c2.IncrementAsync(40, CounterExpiry.Infinite, ct);

            using CancellationTokenSource stoppingCts = new();
            await service.StartAsync(stoppingCts.Token);

            // Act
            await context.Storage.WaitForNextFlushAsync(timeProvider, step: TimeSpan.FromSeconds(5), cancellationToken: ct);

            // Assert
            context.Storage.ShouldHaveValue(c1.Key, 25);
            context.Storage.ShouldHaveValue(c2.Key, 40);
            context.Storage.ShouldHaveBatchFlushCount(1);
            context.Storage.ShouldHaveFlushed(c1.Key, expectedDelta: 25);

            await service.StopAsync(ct);
        }

        [Fact]
        public async Task WhenNoCountersHaveDeltas_DoesNotInvokeBatchIncrement() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AutoFlushInterval = TimeSpan.FromSeconds(5);
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            // Tracked counter exists, but delta is 0
            context.Factory.Create("idle_metric");

            using CancellationTokenSource stoppingCts = new();
            await service.StartAsync(stoppingCts.Token);

            // Act: Advance time to trigger tick
            timeProvider.Advance(TimeSpan.FromSeconds(5));
            await Task.Delay(50, ct);

            // Assert: No increment call dispatched
            Assert.Equal(0, context.Storage.AtomicIncrementCallCount);

            await service.StopAsync(ct);
        }
    }

    public sealed class TheGracefulShutdown {

        [Fact]
        public async Task StopAsync_FlushesRemainingDeltasBeforeHostStops() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AutoFlushInterval = TimeSpan.FromMinutes(10); // Long interval
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter counter = context.Factory.Create("shutdown_metric");
            await counter.IncrementAsync(99, CounterExpiry.Infinite, ct);

            await service.StartAsync(ct);

            // Storage is 0 before stop
            Assert.Equal(0, (await context.Storage.GetAsync(counter.Key, ct)).Value);

            // Act: Trigger graceful host shutdown
            await service.StopAsync(ct);

            // Assert: Final flush flushed the remaining 99 to storage
            Assert.Equal(99, (await context.Storage.GetAsync(counter.Key, ct)).Value);
        }

        [Fact]
        public async Task StopAsync_CalledTwice_DoesNotThrowOrDoubleFlush() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter counter = context.Factory.Create("double_stop_metric");
            await counter.IncrementAsync(42, CounterExpiry.Infinite, ct);

            await service.StartAsync(ct);

            // Act: stop twice in a row
            await service.StopAsync(ct);
            await service.StopAsync(ct); // must not throw

            // Assert
            Assert.Equal(42, (await context.Storage.GetAsync(counter.Key, ct)).Value);
        }
    }

    public sealed class TheFaultToleranceAndRollback {

        [Fact]
        public async Task WhenStorageBatchIncrementFails_RollsBackDeltasToPreventDataLoss() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
            });
            FakeTimeProvider timeProvider = new();

            // Simulate storage failure
            context.Storage.SimulateAtomicIncrementFailure(new TimeoutException("Redis connection lost"));

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter counter = context.Factory.Create("critical_metric");
            await counter.IncrementAsync(150, CounterExpiry.Infinite, ct);

            // Act: Trigger flush during shutdown (which will fail due to simulated exception)
            await service.StopAsync(ct);

            // Assert: Counter MUST retain the 150 in local RAM via rollback! (Zero data loss)
            CounterValue preservedValue = await counter.GetValueAsync(ct);
            Assert.Equal(150, preservedValue.Value);
        }
    }

    public sealed class TheSelfHealingAndDriftTracking {

        [Fact]
        public async Task WhenStorageHasExternalUpdates_SyncsBaseValueWithRealStorageState() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
            });
            FakeTimeProvider timeProvider = new();

            CounterAutoFlushService service = new(
                context.Factory,
                Options.Create(context.Options),
                timeProvider,
                NullLogger<CounterAutoFlushService>.Instance);

            CancellationToken ct = TestContext.Current.CancellationToken;

            IDistributedCounter counter = context.Factory.Create("drifted_metric");
            await counter.IncrementAsync(10, CounterExpiry.Infinite, ct);

            // Pre-seed storage with an external +50 (as if another pod wrote to it)
            context.Storage.SetupGetValue(counter.Key, new CounterValue(50));
            context.Storage.SetupAtomicIncrementResult(counter.Key, new CounterValue(60)); // 50 + 10 = 60

            // Act
            await service.StopAsync(ct);

            // Assert: Base value in counter is synchronized to real storage value (60)
            CounterValue syncedValue = await counter.GetValueAsync(ct);
            Assert.Equal(60, syncedValue.Value);
        }
    }
}