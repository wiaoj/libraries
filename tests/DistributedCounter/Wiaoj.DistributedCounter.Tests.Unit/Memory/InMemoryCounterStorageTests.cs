using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter.Internal.Memory;

namespace Wiaoj.DistributedCounter.Tests.Unit.Memory;

[Trait("Category", "Unit")]
[Trait("Component", "Storage")]
[Trait("Feature", "InMemoryStorage")]
public sealed class InMemoryCounterStorageTests {

    public sealed class TheAtomicIncrementMethod {

        [Fact]
        public async Task GivenNoExpiry_IncrementsCumulatively() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "test:counter";

            // Act
            CounterValue v1 = await storage.AtomicIncrementAsync(key, 5, CounterExpiry.Infinite, TestContext.Current.CancellationToken);
            CounterValue v2 = await storage.AtomicIncrementAsync(key, 10, CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, v1.Value);
            Assert.Equal(15, v2.Value);

            CounterValue current = await storage.GetAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(15, current.Value);
        }

        [Fact]
        public async Task GivenExpiredKey_ResetsCounterToNewAmountOnNextIncrement() {
            // Arrange
            DateTimeOffset startTime = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(startTime);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "test:sliding";

            // Act 1: Initial increment with 10 seconds TTL
            await storage.AtomicIncrementAsync(key, 100, CounterExpiry.FromSeconds(10), TestContext.Current.CancellationToken);

            // Act 2: Advance time past expiration (11 seconds)
            timeProvider.Advance(TimeSpan.FromSeconds(11));

            // Act 3: Increment again with a new amount
            CounterValue afterExpire = await storage.AtomicIncrementAsync(key, 5, CounterExpiry.FromSeconds(10), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, afterExpire.Value); // Old 100 must be wiped out
        }
    }

    public sealed class TheTryIncrementMethod {

        [Fact]
        public async Task WithinLimit_ReturnsAllowedWithCorrectRemainingAndTtl() {
            // Arrange
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            TimeProvider timeProvider = new FakeTimeProvider(now);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "rate:limit";
            CounterExpiry expiry = CounterExpiry.FromSeconds(60);

            // Act: Limit is 10, incrementing by 4
            CounterLimitResult result = await storage.TryIncrementAsync(key, amount: 4, limit: 10, expiry, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(4, result.CurrentValue);
            Assert.Equal(6, result.Remaining);
            Assert.NotNull(result.Ttl);
            Assert.Equal(TimeSpan.FromSeconds(60), result.Ttl.Value);
        }

        [Fact]
        public async Task ExceedingLimit_ReturnsDeniedWithCurrentValueAndLiveTtl() {
            // Arrange
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "rate:limit";
            CounterExpiry expiry = CounterExpiry.FromSeconds(60);

            // Act 1: Reach 8 out of 10
            await storage.TryIncrementAsync(key, amount: 8, limit: 10, expiry, TestContext.Current.CancellationToken);

            // Advance 10 seconds into the 60s window
            timeProvider.Advance(TimeSpan.FromSeconds(10));

            // Act 2: Attempt to increment by 3 (8 + 3 = 11 > 10, rejected!)
            CounterLimitResult rejected = await storage.TryIncrementAsync(key, amount: 3, limit: 10, expiry, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(rejected.IsAllowed);
            Assert.Equal(8, rejected.CurrentValue); // Value not mutated
            Assert.Equal(0, rejected.Remaining);
            Assert.NotNull(rejected.Ttl);
            Assert.Equal(TimeSpan.FromSeconds(50), rejected.Ttl.Value); // 50 seconds remaining in window
        }

        [Fact]
        public async Task AfterWindowExpires_ResetsQuotaAutomatically() {
            // Arrange
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "rate:limit";

            // Act 1: Max out limit (10/10)
            await storage.TryIncrementAsync(key, 10, 10, CounterExpiry.FromSeconds(30), TestContext.Current.CancellationToken);

            // Advance time past the 30s window
            timeProvider.Advance(TimeSpan.FromSeconds(31));

            // Act 2: Try increment again in the new window
            CounterLimitResult newWindowResult = await storage.TryIncrementAsync(key, 2, 10, CounterExpiry.FromSeconds(30), TestContext.Current.CancellationToken);

            // Assert
            Assert.True(newWindowResult.IsAllowed);
            Assert.Equal(2, newWindowResult.CurrentValue);
            Assert.Equal(8, newWindowResult.Remaining);
        }

        [Fact]
        public async Task WithZeroAmount_IsAllowedAndDoesNotChangeValue() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "zero:increment";
            CancellationToken ct = TestContext.Current.CancellationToken;
            await storage.SetAsync(key, new CounterValue(5), CounterExpiry.Infinite, ct);

            // Act
            CounterLimitResult result = await storage.TryIncrementAsync(key, amount: 0, limit: 10, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(5, result.CurrentValue);
            Assert.Equal(5, result.Remaining);
        }
    }

    public sealed class TheTryDecrementMethod {

        [Fact]
        public async Task AboveMinLimit_DecrementsSuccessfully() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "slots:available";

            // Pre-seed storage with 10 slots
            await storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            // Act: Decrement 4 slots with minLimit of 0
            CounterLimitResult result = await storage.TryDecrementAsync(key, amount: 4, minLimit: 0, CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(6, result.CurrentValue);
            Assert.Equal(6, result.Remaining); // 6 - 0 = 6 slots left
        }

        [Fact]
        public async Task DroppingBelowMinLimit_RejectsOperation() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "slots:available";

            // Pre-seed with 2
            await storage.SetAsync(key, new CounterValue(2), CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            // Act: Try to consume 5 when minimum allowed is 0 (2 - 5 = -3 < 0)
            CounterLimitResult result = await storage.TryDecrementAsync(key, amount: 5, minLimit: 0, CounterExpiry.Infinite, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(result.IsAllowed);
            Assert.Equal(2, result.CurrentValue); // Unchanged
            Assert.Equal(0, result.Remaining);
        }

        [Fact]
        public async Task AfterWindowExpires_ResetsQuotaAutomatically() {
            // Arrange
            DateTimeOffset now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "decrement:window";
            CancellationToken ct = TestContext.Current.CancellationToken;

            await storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, ct);

            // Act 1: consume down to min limit within a 30s window
            await storage.TryDecrementAsync(key, amount: 10, minLimit: 0, CounterExpiry.FromSeconds(30), ct);

            // Advance past the window
            timeProvider.Advance(TimeSpan.FromSeconds(31));

            // Act 2: fresh decrement in a brand-new window
            await storage.SetAsync(key, new CounterValue(10), CounterExpiry.Infinite, ct);
            CounterLimitResult newWindowResult = await storage.TryDecrementAsync(key, amount: 3, minLimit: 0, CounterExpiry.FromSeconds(30), ct);

            // Assert
            Assert.True(newWindowResult.IsAllowed);
            Assert.Equal(7, newWindowResult.CurrentValue);
        }

        [Fact]
        public async Task WithZeroAmount_IsAllowedAndDoesNotChangeValue() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "zero:decrement";
            CancellationToken ct = TestContext.Current.CancellationToken;
            await storage.SetAsync(key, new CounterValue(5), CounterExpiry.Infinite, ct);

            // Act
            CounterLimitResult result = await storage.TryDecrementAsync(key, amount: 0, minLimit: 0, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(5, result.CurrentValue);
        }
    }

    public sealed class TheConcurrencyAndRaceConditions {

        [Fact]
        public async Task ConcurrentAtomicIncrements_PreserveTotalCountWithoutLoss() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "concurrent:metric";
            const int concurrency = 50;
            const int incrementsPerTask = 200;

            // Act: 50 tasks each incrementing 200 times by 1 (Total should be 10,000)
            Task[] tasks = Enumerable.Range(0, concurrency)
                .Select(_ => Task.Run(async () => {
                    for(int i = 0; i < incrementsPerTask; i++) {
                        await storage.AtomicIncrementAsync(key, 1, CounterExpiry.Infinite, TestContext.Current.CancellationToken);
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            CounterValue finalValue = await storage.GetAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(concurrency * incrementsPerTask, finalValue.Value);
        }

        [Fact]
        public async Task ConcurrentTryIncrements_NeverExceedStrictLimit() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);
            CounterKey key = "concurrent:quota";
            const int limit = 50;
            const int totalAttempts = 200;

            int allowedCount = 0;

            // Act: 200 concurrent tasks competing for 50 quota spots
            Task[] tasks = Enumerable.Range(0, totalAttempts)
                .Select(_ => Task.Run(async () => {
                    CounterLimitResult res = await storage.TryIncrementAsync(key, 1, limit, CounterExpiry.Infinite, TestContext.Current.CancellationToken);
                    if(res.IsAllowed) {
                        Interlocked.Increment(ref allowedCount);
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert
            CounterValue finalValue = await storage.GetAsync(key, TestContext.Current.CancellationToken);
            Assert.Equal(limit, allowedCount);
            Assert.Equal(limit, finalValue.Value);
        }
    }

    public sealed class TheBatchAndTtlOperations {

        [Fact]
        public async Task BatchIncrementAsync_UpdatesMultipleKeysAtomically() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);

            CounterUpdate[] updates = [
                new CounterUpdate(new CounterKey("batch:k1"), 5, CounterExpiry.Infinite),
                new CounterUpdate(new CounterKey("batch:k2"), 15, CounterExpiry.Infinite),
                new CounterUpdate(new CounterKey("batch:k1"), 10, CounterExpiry.Infinite) // Second increment to same key
            ];

            long[] results = new long[3];

            // Act
            await storage.BatchIncrementAsync(updates.AsMemory(), results.AsMemory(), TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(5, results[0]);
            Assert.Equal(15, results[1]);
            Assert.Equal(15, results[2]); // 5 + 10 = 15

            CounterValue k1Val = await storage.GetAsync(new CounterKey("batch:k1"), TestContext.Current.CancellationToken);
            Assert.Equal(15, k1Val.Value);
        }

        [Fact]
        public async Task GetTtlAsync_ReturnsNullForMissingOrInfiniteKeys() {
            // Arrange
            TimeProvider timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            InMemoryCounterStorage storage = new(timeProvider);

            // Act & Assert (Missing key)
            TimeSpan? missingTtl = await storage.GetTtlAsync(new CounterKey("non:existing"), TestContext.Current.CancellationToken);
            Assert.Null(missingTtl);

            // Act & Assert (Infinite key)
            await storage.AtomicIncrementAsync("infinite:key", 1, CounterExpiry.Infinite, TestContext.Current.CancellationToken);
            TimeSpan? infiniteTtl = await storage.GetTtlAsync("infinite:key", TestContext.Current.CancellationToken);
            Assert.Null(infiniteTtl);
        }
    }
}