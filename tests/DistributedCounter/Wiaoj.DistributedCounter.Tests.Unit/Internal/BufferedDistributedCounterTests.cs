using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Internal")]
[Trait("Feature", "BufferedCounter")]
public sealed class BufferedDistributedCounterTests {

    public sealed class TheHydrationAndIncrement {

        [Fact]
        public async Task FirstOperation_HydratesBaseValueFromStorageExactlyOnce() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:users";
            storage.SetupGetValue(key, new CounterValue(50)); // Remote starts at 50

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act 1: First increment of 5
            CounterValue v1 = await counter.IncrementAsync(5, CounterExpiry.Infinite, ct);

            // Act 2: Second increment of 10
            CounterValue v2 = await counter.IncrementAsync(10, CounterExpiry.Infinite, ct);

            // Assert
            Assert.Equal(55, v1.Value); // 50 (base) + 5
            Assert.Equal(65, v2.Value); // 50 (base) + 15
            Assert.Equal(1, storage.GetCallCount); // Storage.GetAsync called only ONCE!
            Assert.Equal(0, storage.AtomicIncrementCallCount); // Remote increment not invoked (buffered in RAM)
        }

        [Fact]
        public async Task Decrement_SubtractsFromLocalDelta() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:stock";
            storage.SetupGetValue(key, new CounterValue(100));

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterValue remaining = await counter.DecrementAsync(15, CounterExpiry.Infinite, ct);

            // Assert
            Assert.Equal(85, remaining.Value);
        }
    }

    public sealed class TheLimitOperationsWithAutoFlush {

        [Fact]
        public async Task TryIncrement_FlushesLocalDeltaBeforeEvaluatingLimitInStorage() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:limit";
            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Accumulate 4 in local RAM (Storage is still 0)
            await counter.IncrementAsync(4, CounterExpiry.Infinite, ct);
            Assert.Equal(0, (await storage.GetAsync(key, ct)).Value);

            // Act: Try adding +3 with limit 10 (4 + 3 = 7 <= 10 -> Should be allowed)
            CounterLimitResult result = await counter.TryIncrementAsync(3, limit: 10, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(7, result.CurrentValue);
            Assert.Equal(3, result.Remaining); // 10 - 7 = 3

            // Storage must be synchronized to 7 and local base updated
            Assert.Equal(7, (await storage.GetAsync(key, ct)).Value);
            Assert.Equal(7, (await counter.GetValueAsync(ct)).Value);
        }

        [Fact]
        public async Task TryDecrement_FlushesLocalDeltaBeforeEvaluatingMinLimitInStorage() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:min_limit";
            await storage.SetAsync(key, new CounterValue(20), CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Decrement 5 from RAM (Local delta = -5)
            await counter.DecrementAsync(5, CounterExpiry.Infinite, ct);

            // Act: Try decrementing 6 with minLimit 5 (15 - 6 = 9 >= 5 -> Should be allowed)
            CounterLimitResult result = await counter.TryDecrementAsync(6, minLimit: 5, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(9, result.CurrentValue);
            Assert.Equal(4, result.Remaining); // 9 - 5 = 4
        }

        [Fact]
        public async Task TryIncrement_WhenRejectedAfterFlush_LocalDeltaMatchesRealUnchangedStorageValue() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:limit:rejected";
            storage.SetupGetValue(key, new CounterValue(8));
            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterLimitResult result = await counter.TryIncrementAsync(5, limit: 10, CounterExpiry.Infinite, ct);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(8, result.CurrentValue);
            Assert.Equal(8, (await storage.GetAsync(key, ct)).Value);
            Assert.Equal(8, (await counter.GetValueAsync(ct)).Value);

            CounterValue afterLegitIncrement = await counter.IncrementAsync(1, CounterExpiry.Infinite, ct);
            Assert.Equal(9, afterLegitIncrement.Value);
        }

        [Fact]
        public async Task TryDecrement_WhenRejectedAfterFlush_LocalDeltaMatchesRealUnchangedStorageValue() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:minlimit:rejected";
            storage.SetupGetValue(key, new CounterValue(6));
            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterLimitResult result = await counter.TryDecrementAsync(5, minLimit: 2, CounterExpiry.Infinite, ct);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(6, result.CurrentValue);
            Assert.Equal(6, (await counter.GetValueAsync(ct)).Value);
            Assert.Equal(6, (await storage.GetAsync(key, ct)).Value);
        }
    }

    public sealed class TheSetAndExpiryOperations {

        [Fact]
        public async Task SetAsync_ClearsLocalDelta_AndOverwritesStorageAndBaseValue() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:set";
            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            await counter.IncrementAsync(30, CounterExpiry.Infinite, ct); // Local delta is 30

            // Act: Overwrite with absolute 100
            await counter.SetAsync(100, CounterExpiry.Infinite, ct);

            // Assert
            Assert.Equal(1, storage.SetCallCount);
            Assert.Equal(100, (await storage.GetAsync(key, ct)).Value);
            Assert.Equal(100, (await counter.GetValueAsync(ct)).Value);
        }

        [Fact]
        public async Task IncrementWithExpiry_CapturesExpiryTicksDuringFlush() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:expiry";
            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;

            CounterExpiry customExpiry = CounterExpiry.FromMinutes(10);

            // Act
            await counter.IncrementAsync(15, customExpiry, ct);

            // Capture delta and verify TTL propagation
            bool captured = counter.TryCaptureDelta(out long delta, out CounterExpiry capturedExpiry);

            // Assert
            Assert.True(captured);
            Assert.Equal(15, delta);
            Assert.Equal(customExpiry.Value, capturedExpiry.Value);
        }
    }

    public sealed class TheFlushAndRollbackMechanisms {

        [Fact]
        public async Task FlushAsync_PushesDeltaToStorage_AndUpdatesBaseValue() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:metrics";
            storage.SetupGetValue(key, CounterValue.Zero);
            storage.SetupAtomicIncrementResult(key, new CounterValue(30));

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;
            await counter.IncrementAsync(30, CounterExpiry.Infinite, ct);

            // Act
            await counter.FlushAsync(ct);

            // Assert
            Assert.Equal(1, storage.AtomicIncrementCallCount);
            Assert.Equal(30, counter.GetCurrentBaseValue());

            // Next GetValueAsync reads clean base value with 0 local delta
            CounterValue current = await counter.GetValueAsync(ct);
            Assert.Equal(30, current.Value);
        }

        [Fact]
        public async Task FlushAsync_WhenStorageFails_RollsBackDeltaWithoutDataLoss() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:critical";
            storage.SetupGetValue(key, CounterValue.Zero);
            storage.SimulateAtomicIncrementFailure(new TimeoutException("Redis timed out"));

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;
            await counter.IncrementAsync(50, CounterExpiry.Infinite, ct);

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(() => counter.FlushAsync(ct).AsTask());

            // The 50 delta MUST be rolled back into local memory!
            CounterValue preservedValue = await counter.GetValueAsync(ct);
            Assert.Equal(50, preservedValue.Value);
        }
    }

    public sealed class TheSelfHealingAndDrift {

        [Fact]
        public void SyncWithStorage_DetectsDriftFromOtherNodesCorrectly() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:drift";
            BufferedDistributedCounter counter = new(key, storage);

            // Simulate that local base was 100, and we flushed a delta of +20
            counter.CommitDelta(100); // Base is now 100

            // Act: Storage returns 130 (another node added +10 in between!)
            long drift = counter.SyncWithStorage(redisRealValue: 130, justFlushedDelta: 20);

            // Assert: Expected 100 + 20 = 120. Actual 130. Drift: +10
            Assert.Equal(10, drift);
            Assert.Equal(130, counter.GetCurrentBaseValue());
        }
    }

    public sealed class TheStateReset {

        [Fact]
        public async Task ResetAsync_ClearsLocalState_AndDeletesFromStorage() {
            // Arrange
            FakeCounterStorage storage = new();
            CounterKey key = "buffered:reset";
            storage.SetupGetValue(key, new CounterValue(100));

            BufferedDistributedCounter counter = new(key, storage);
            CancellationToken ct = TestContext.Current.CancellationToken;
            await counter.IncrementAsync(25, CounterExpiry.Infinite, ct);

            // Act
            await counter.ResetAsync(ct);

            // Assert
            Assert.Equal(1, storage.DeleteCallCount);
            Assert.Equal(0, counter.GetCurrentBaseValue());

            CounterValue current = await counter.GetValueAsync(ct);
            Assert.Equal(0, current.Value);
        }
    }
}