using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Internal.Memory;
using Wiaoj.DistributedCounter.Testing;

namespace Wiaoj.DistributedCounter.Tests.Unit.Memory;

[Trait("Category", "Unit")]
[Trait("Component", "Storage")]
[Trait("Feature", "CompareExchange")]
public sealed class InMemoryCounterStorageCasTests {

    public sealed class TheBasicCasOperations {

        [Fact]
        public async Task TryCompareExchange_WhenValueMatches_UpdatesValueAndReturnsTrue() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:basic:match";
            CancellationToken ct = TestContext.Current.CancellationToken;

            await storage.SetAsync(key, new CounterValue(100), CounterExpiry.Infinite, ct);

            // Act: Expect 100, update to 150
            bool success = await storage.TryCompareExchangeAsync(key, expectedValue: 100, newValue: 150, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(success);
            Assert.Equal(150, (await storage.GetAsync(key, ct)).Value);
        }

        [Fact]
        public async Task TryCompareExchange_WhenValueMismatches_LeavesStorageUntouchedAndReturnsFalse() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:basic:mismatch";
            CancellationToken ct = TestContext.Current.CancellationToken;

            await storage.SetAsync(key, new CounterValue(100), CounterExpiry.Infinite, ct);

            // Act: Expect 999 (wrong), attempt change to 200
            bool success = await storage.TryCompareExchangeAsync(key, expectedValue: 999, newValue: 200, CounterExpiry.Infinite, ct);

            // Assert
            Assert.False(success);
            Assert.Equal(100, (await storage.GetAsync(key, ct)).Value);
        }

        [Fact]
        public async Task TryCompareExchange_OnNonExistentKey_TreatsValueAsZero() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:nonexistent";
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Expect 0 on a brand new key, initialize to 50
            bool success = await storage.TryCompareExchangeAsync(key, expectedValue: 0, newValue: 50, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(success);
            Assert.Equal(50, (await storage.GetAsync(key, ct)).Value);
        }

        [Fact]
        public async Task TryCompareExchange_OnExpiredKey_TreatsValueAsZeroAndAppliesNewExpiry() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:expired";
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Seed with 10s TTL
            await storage.SetAsync(key, new CounterValue(500), CounterExpiry.FromSeconds(10), ct);

            // Advance time past expiration
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // Act: Since expired, expect 0 and revive with 42
            bool success = await storage.TryCompareExchangeAsync(key, expectedValue: 0, newValue: 42, CounterExpiry.FromSeconds(30), ct);

            // Assert
            Assert.True(success);
            Assert.Equal(42, (await storage.GetAsync(key, ct)).Value);
            Assert.NotNull(await storage.GetTtlAsync(key, ct));
        }
    }

    public sealed class TheConcurrencyAndRaceConditions {

        [Fact]
        public async Task ConcurrentCas_WhenMultipleThreadsRaceToTransitionState_ExactlyOneSucceeds() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:race:single_winner";
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Initial state: 0 (e.g. State: Pending)
            await storage.SetAsync(key, CounterValue.Zero, CounterExpiry.Infinite, ct);

            const int concurrency = 50;
            int successfulTransitions = 0;

            // Act: 50 threads racing to transition from 0 to 1 (State: Processing)
            Task[] tasks = [.. Enumerable.Range(0, concurrency)
                .Select(i => Task.Run(async () => {
                    bool result = await storage.TryCompareExchangeAsync(
                        key,
                        expectedValue: CounterValue.Zero,
                        newValue: new CounterValue(1),
                        CounterExpiry.Infinite,
                        ct);

                    if(result) {
                        Interlocked.Increment(ref successfulTransitions);
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            // Assert: Exactly ONE thread won the CAS race! Zero race-condition breaches.
            Assert.Equal(1, successfulTransitions);
            Assert.Equal(1, (await storage.GetAsync(key, ct)).Value);
        }

        [Fact]
        public async Task ConcurrentCasLoop_WhenThreadsIncrementViaOptimisticLocking_PreservesExactTotal() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "cas:race:optimistic_loop";
            CancellationToken ct = TestContext.Current.CancellationToken;

            await storage.SetAsync(key, CounterValue.Zero, CounterExpiry.Infinite, ct);

            const int threadCount = 20;
            const int incrementsPerThread = 50;
            const int expectedTotal = threadCount * incrementsPerThread;

            // Act: 20 threads performing CAS retry-loops (optimistic concurrency)
            Task[] tasks = [.. Enumerable.Range(0, threadCount)
                .Select(_ => Task.Run(async () => {
                    for(int i = 0; i < incrementsPerThread; i++) {
                        while(!ct.IsCancellationRequested) {
                            CounterValue current = await storage.GetAsync(key, ct);
                            CounterValue next = current + 1;

                            if(await storage.TryCompareExchangeAsync(key, current, next, CounterExpiry.Infinite, ct)) {
                                break; // Successfully updated, move to next increment
                            }
                        }
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            // Assert: Zero lost updates despite massive optimistic retry contention
            Assert.Equal(expectedTotal, (await storage.GetAsync(key, ct)).Value);
        }
    }

    public sealed class TheTypedTagAndScopedKeyCas {

        [Fact]
        public async Task TypedWrapper_ForKey_ExecutesCasOnSpecificScopedIdentity() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.AddImmediateCounter<StateMarkerTag>();
            });

            IDistributedCounterFactory factory = context.CreateFactory();
            IDistributedCounter<StateMarkerTag> typedCounter = new TypedDistributedCounterWrapper<StateMarkerTag>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            string orderId = "order_12345";

            // Act: State machine transition for order_12345: 0 (Created) -> 1 (Paid)
            bool createdToPaid = await typedCounter.TryCompareExchangeAsync(
                orderId,
                expectedValue: 0,
                newValue: 1,
                CounterExpiry.Infinite,
                ct);

            // Attempt invalid transition: 0 (Created) -> 2 (Shipped) - Must fail because it's now 1!
            bool invalidTransition = await typedCounter.TryCompareExchangeAsync(
                orderId,
                expectedValue: 0,
                newValue: 2,
                CounterExpiry.Infinite,
                ct);

            // Assert
            Assert.True(createdToPaid);
            Assert.False(invalidTransition);
            Assert.Equal(1, (await typedCounter.GetValueAsync(orderId, ct)).Value);
        }

        private sealed class StateMarkerTag;
    }
}