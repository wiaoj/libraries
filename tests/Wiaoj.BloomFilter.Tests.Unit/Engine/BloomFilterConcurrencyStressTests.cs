using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IO;
using Wiaoj.BloomFilter.Engine;
using Wiaoj.BloomFilter.Testing;
using Wiaoj.Primitives;

namespace Wiaoj.BloomFilter.Tests.Unit.Engine;

public class BloomFilterConcurrencyStressTests {
    private readonly BloomFilterConfigurationFactory _configFactory = new();

    internal BloomFilterContext CreateContext(TimeProvider? timeProvider = null, FakeBloomFilterStorage? storage = null) {
        return new BloomFilterContext(
            Storage: storage ?? new FakeBloomFilterStorage(),
            RecyclableMemoryStreamManager: new RecyclableMemoryStreamManager(),
            Logger: NullLogger.Instance,
            Options: new BloomFilterOptions(),
            TimeProvider: timeProvider ?? TimeProvider.System,
            ConfigFactory: this._configFactory
        );
    }

    public sealed class InMemoryFilterStress : BloomFilterConcurrencyStressTests {
        [Fact]
        public async Task Should_HandleHighContentionWritersAndReaders_WithoutDataLossOrExceptions() {
            // Arrange: 32 concurrent writers adding 500 items each (16,000 total items)
            const int writerCount = 32;
            const int itemsPerWriter = 500;
            const int readerCount = 16;
            const int totalItems = writerCount * itemsPerWriter;

            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("stress-in-memory"), totalItems, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            using Barrier barrier = new(writerCount + readerCount);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Start concurrent writers and readers simultaneously
            Task[] writerTasks = Enumerable.Range(0, writerCount).Select(writerIndex => Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < itemsPerWriter; i++) {
                    filter.Add($"thread-{writerIndex}-item-{i}");
                }
            }, ct)).ToArray();

            Task[] readerTasks = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < itemsPerWriter; i++) {
                    bool isContains = filter.Contains($"non-existent-probe-{i}");
                }
            }, ct)).ToArray();

            await Task.WhenAll([.. writerTasks, .. readerTasks]);

            // Assert: Every single item written by all concurrent threads must exist
            for(int writerIndex = 0; writerIndex < writerCount; writerIndex++) {
                for(int i = 0; i < itemsPerWriter; i++) {
                    Assert.True(filter.Contains($"thread-{writerIndex}-item-{i}"), $"Item from writer {writerIndex} index {i} was lost.");
                }
            }

            Assert.True(filter.GetPopCount() > 0);
        }
    }

    public sealed class ScalableFilterLayerExpansionStress : BloomFilterConcurrencyStressTests {
        [Fact]
        public async Task Should_ScaleDynamicLayersSafely_When_MultipleThreadsCauseSaturationSimultaneously() {
            // Arrange: Small initial capacity (500 items) with 50% threshold to force multiple rapid ScaleUp() calls
            const int threadCount = 16;
            const int itemsPerThread = 1_000; // 16,000 items total will force multiple dynamic layers

            BloomFilterContext context = CreateContext();
            BloomFilterConfiguration baseConfig = this._configFactory.Create(FilterName.Parse("stress-scalable"), 500, 0.01);
            using ScalableBloomFilter filter = new(
                baseConfig,
                context,
                growthRate: GrowthRate.Double,
                saturationThreshold: Percentage.FromDouble(0.50));

            using Barrier barrier = new(threadCount);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Bombard scalable filter from multiple threads forcing parallel ScaleUp double-checked lock transitions
            Task[] tasks = Enumerable.Range(0, threadCount).Select(threadId => Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < itemsPerThread; i++) {
                    filter.Add($"scalable-t{threadId}-key-{i}");
                    if(i % 10 == 0) {
                        _ = filter.Contains($"scalable-t{threadId}-key-{i}");
                    }
                }
            }, ct)).ToArray();

            await Task.WhenAll(tasks);

            // Assert: All 16,000 items must be discoverable across all dynamically created layers
            for(int threadId = 0; threadId < threadCount; threadId++) {
                for(int i = 0; i < itemsPerThread; i++) {
                    Assert.True(filter.Contains($"scalable-t{threadId}-key-{i}"), $"Item scalable-t{threadId}-key-{i} was missing after scaling.");
                }
            }
        }
    }

    public sealed class RotatingFilterWindowRotationStress : BloomFilterConcurrencyStressTests {
        [Fact]
        public async Task Should_MaintainThreadSafety_When_TimeAdvancesDuringConcurrentAccess() {
            // Arrange: 3 shards for a 3-hour window (1 hour per shard)
            FakeTimeProvider fakeTime = new();
            fakeTime.SetUtcNow(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));

            BloomFilterContext context = CreateContext(timeProvider: fakeTime);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("stress-rotating"), 10_000, 0.01);
            using RotatingBloomFilter filter = new(config, context, windowSize: TimeSpan.FromHours(3), shardCount: 3);

            const int workerCount = 8;
            using Barrier barrier = new(workerCount + 1);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Concurrently add items while advancing time to force time shard rotation
            Task[] workerTasks = Enumerable.Range(0, workerCount).Select(id => Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < 500; i++) {
                    filter.Add($"rot-item-w{id}-{i}");
                    _ = filter.Contains($"rot-item-w{id}-{i}");
                }
            }, ct)).ToArray();

            Task timerTask = Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < 5; i++) {
                    fakeTime.Advance(TimeSpan.FromMinutes(45));
                    Thread.Sleep(5);
                }
            }, ct);

            await Task.WhenAll([.. workerTasks, timerTask]);

            // Assert: Filter must remain intact and functional without deadlocks or unhandled concurrency faults
            filter.Add("post-rotation-item");
            Assert.True(filter.Contains("post-rotation-item"));
        }
    }

    public sealed class ConcurrentPersistenceStress : BloomFilterConcurrencyStressTests {
        [Fact]
        public async Task Should_NotCorruptState_When_SaveAsyncExecutesConcurrentlyWithContinuousWrites() {
            // Arrange
            FakeBloomFilterStorage storage = new();
            BloomFilterContext context = CreateContext(storage: storage);
            BloomFilterConfiguration config = this._configFactory.Create(FilterName.Parse("stress-save-race"), 20_000, 0.01);
            using InMemoryBloomFilter filter = new(config, context);

            const int writerThreads = 8;
            const int itemsPerThread = 1_000;
            using Barrier barrier = new(writerThreads + 1);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Continuously write items while concurrently calling SaveAsync multiple times
            Task[] writers = Enumerable.Range(0, writerThreads).Select(w => Task.Run(() => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < itemsPerThread; i++) {
                    filter.Add($"writer-{w}-item-{i}");
                }
            }, ct)).ToArray();

            Task saver = Task.Run(async () => {
                barrier.SignalAndWait(ct);
                for(int i = 0; i < 5; i++) {
                    await filter.SaveAsync(ct);
                    await Task.Delay(5, ct);
                }
            }, ct);

            await Task.WhenAll([.. writers, saver]);

            // Final save to flush any remaining dirty bits
            await filter.SaveAsync(ct);

            // Assert: Storage must have captured valid snapshot and reload must find elements
            using InMemoryBloomFilter reloaded = new(config, context);
            await reloaded.ReloadAsync(ct);

            for(int w = 0; w < writerThreads; w++) {
                Assert.True(reloaded.Contains($"writer-{w}-item-0"));
                Assert.True(reloaded.Contains($"writer-{w}-item-500"));
            }
        }
    }
}