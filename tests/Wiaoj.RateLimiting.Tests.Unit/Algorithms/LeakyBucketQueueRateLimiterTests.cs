using Microsoft.Extensions.Time.Testing;
using Wiaoj.RateLimiting.Tests.Unit.Fakes;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "LeakyBucketQueue")]
public sealed class LeakyBucketQueueRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeCapacity_ThrowsArgumentOutOfRangeException(int invalidCapacity) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new LeakyBucketQueueRateLimiter(invalidCapacity, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativePeriod_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new LeakyBucketQueueRateLimiter(10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheTryAcquireArgumentValidation {

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5));

            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => limiter.TryAcquireAsync(invalidKey!, cost: 1, TestContext.Current.CancellationToken).AsTask());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5));

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => limiter.TryAcquireAsync("client_1", invalidCost, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheQueueSchedulingAndTrafficShaping {

        [Fact]
        public async Task FirstRequestOnIdleQueue_ExecutesImmediatelyWithoutDelay() {
            // Arrange: 5 capacity per 5 seconds (1 unit per second)
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_queue_1";

            // Act: First request
            ValueTask<RateLimitDecision> task = limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert: Completed synchronously without delay
            Assert.True(task.IsCompletedSuccessfully);
            RateLimitDecision decision = await task;
            Assert.True(decision.IsAllowed);
            Assert.Equal(4, decision.Remaining);
        }

        [Fact]
        public async Task EnqueuedRequest_AwaitsUntilItsScheduledSlotArrives() {
            // Arrange: 5 capacity per 5 seconds (1 unit per second)
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_queue_2";

            // Request 1 takes slot at t=0s
            await limiter.TryAcquireAsync(key, cost: 1, ct);

            // Act 1: Request 2 scheduled at t=1s (must wait 1 second)
            ValueTask<RateLimitDecision> queuedTask = limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert 1: Suspended/Waiting
            Assert.False(queuedTask.IsCompleted);

            // Act 2: Advance time by 1 second to release the queued turn
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            RateLimitDecision decision = await queuedTask;

            // Assert 2: Completed and allowed
            Assert.True(decision.IsAllowed);
        }

        [Fact]
        public async Task BacklogExceedingCapacity_IsRejectedImmediatelyWithoutWaiting() {
            // Arrange: Max queue capacity is 2
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 2, period: TimeSpan.FromSeconds(2), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_queue_full";

            // Fill backlog (Slot 1 at t=0s, Slot 2 at t=1s)
            await limiter.TryAcquireAsync(key, cost: 1, ct);
            ValueTask<RateLimitDecision> slot2 = limiter.TryAcquireAsync(key, cost: 1, ct);

            // Act: Request 3 arrives when backlog is completely full (2/2)
            ValueTask<RateLimitDecision> overflowTask = limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert: Denied immediately (synchronous rejection without waiting!)
            Assert.True(overflowTask.IsCompletedSuccessfully);
            RateLimitDecision overflowDecision = await overflowTask;
            Assert.False(overflowDecision.IsAllowed);
            Assert.NotNull(overflowDecision.RetryAfter);

            // Clean up suspended task
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await slot2;
        }

        [Fact]
        public async Task RequestWithCostGreaterThanCapacity_IsRejectedImmediately() {
            LeakyBucketQueueRateLimiter limiter = new(capacity: 3, period: TimeSpan.FromSeconds(3));
            CancellationToken ct = TestContext.Current.CancellationToken;

            RateLimitDecision decision = await limiter.TryAcquireAsync("client_over", cost: 5, ct);

            Assert.False(decision.IsAllowed);
            Assert.Equal(3, decision.Remaining);
        }
    }

    public sealed class TheCancellationAndRollback {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledExceptionImmediately() {
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => limiter.TryAcquireAsync("client_precancelled", cost: 1, cts.Token).AsTask());
        }

        [Fact]
        public async Task WhenQueuedRequestIsCancelled_RollsBackReservationInBacklog() {
            // Arrange: 5 capacity per 5 seconds
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            string key = "client_cancel";

            // Request 1 at t=0s
            RateLimitDecision r1 = await limiter.TryAcquireAsync(key, cost: 1, TestContext.Current.CancellationToken);
            Assert.True(r1.IsAllowed);

            using CancellationTokenSource cts = new();

            // Request 2 queues for slot at t=1s
            ValueTask<RateLimitDecision> queuedTask = limiter.TryAcquireAsync(key, cost: 1, cts.Token);
            Assert.False(queuedTask.IsCompleted);

            // Act: Cancel Request 2 while it's still waiting
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queuedTask.AsTask());

            // Assert: Reservation rolled back! A new request at t=0s takes the freed slot immediately
            timeProvider.Advance(TimeSpan.FromSeconds(1));
            ValueTask<RateLimitDecision> nextTask = limiter.TryAcquireAsync(key, cost: 1, CancellationToken.None);
            Assert.True(nextTask.IsCompletedSuccessfully);
            Assert.True((await nextTask).IsAllowed);
        }

        [Fact]
        public async Task WhenQueuedRequestIsCancelled_NextRequestReclaimsFreedSlot_WithoutExtraWait() {
            // Arrange: 5 capacity per 5 seconds -> 1 slot/sec
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 5, period: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            string key = "client_cancel_rollback";

            // Request 1 at t=0s: admitted immediately, occupies the [0s, 1s) slot
            RateLimitDecision r1 = await limiter.TryAcquireAsync(key, cost: 1, CancellationToken.None);
            Assert.True(r1.IsAllowed);

            using CancellationTokenSource cts = new();

            // Request 2 at t=0s: queues for the [1s, 2s) slot
            ValueTask<RateLimitDecision> request2Task = limiter.TryAcquireAsync(key, cost: 1, cts.Token);
            Assert.False(request2Task.IsCompleted);

            // Act: cancel Request 2 while it still occupies the [1s, 2s) slot
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request2Task.AsTask());

            // Request 3 at t=0s, right after the cancellation.
            ValueTask<RateLimitDecision> request3Task = limiter.TryAcquireAsync(key, cost: 1, CancellationToken.None);
            Assert.False(request3Task.IsCompleted);

            // Advance just short of 1s: Request 3 must still be waiting.
            timeProvider.Advance(TimeSpan.FromMilliseconds(900));
            Assert.False(request3Task.IsCompleted);

            // Advance past the 1s mark: Request 3 must now complete.
            timeProvider.Advance(TimeSpan.FromMilliseconds(200));
            RateLimitDecision result3 = await request3Task;
            Assert.True(result3.IsAllowed);
        }
    }

    public sealed class TheKeyIsolation {

        [Fact]
        public async Task DifferentKeys_MaintainIndependentQueuesAndSchedules() {
            FakeTimeProvider timeProvider = new();
            LeakyBucketQueueRateLimiter limiter = new(capacity: 2, period: TimeSpan.FromSeconds(2), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Fill queue for Key A
            await limiter.TryAcquireAsync("client_a", cost: 1, ct);
            ValueTask<RateLimitDecision> aQueued = limiter.TryAcquireAsync("client_a", cost: 1, ct);

            // Key B executes immediately on its own idle queue
            ValueTask<RateLimitDecision> bTask = limiter.TryAcquireAsync("client_b", cost: 1, ct);

            Assert.False(aQueued.IsCompleted);
            Assert.True(bTask.IsCompletedSuccessfully);
            Assert.True((await bTask).IsAllowed);

            timeProvider.Advance(TimeSpan.FromSeconds(1));
            await aQueued;
        }
    }

    public sealed class TheClockSkewAndNtpImmunity {

        [Fact]
        public async Task WhenSystemClockJumpsBackward_NewRequestsAreNotBlockedByDrift() {
            // Arrange
            FakeTimeProvider fakeTime = new();
            ClockSkewTimeProvider timeProvider = new(fakeTime);
            LeakyBucketQueueRateLimiter limiter = new(capacity: 2, period: TimeSpan.FromSeconds(2), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "leaky_ntp_subsequent";

            // First request consumes initial slot at T0
            RateLimitDecision first = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.True(first.IsAllowed);

            // Act
            // System wall-clock jumps backward by 1 hour
            timeProvider.WallClockOffset = TimeSpan.FromHours(-1);

            // 2 seconds elapse in physical monotonic time (draining the backlog)
            fakeTime.Advance(TimeSpan.FromSeconds(2));

            // Subsequent request arrives after clock skew
            RateLimitDecision second = await limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert
            Assert.True(second.IsAllowed);
        }

        [Fact]
        public async Task WhenSystemClockJumpsBackward_QueuedTasksExecuteOnAccurateMonotonicIntervals() {
            // Arrange
            FakeTimeProvider fakeTime = new();
            ClockSkewTimeProvider timeProvider = new(fakeTime);
            LeakyBucketQueueRateLimiter limiter = new(capacity: 2, period: TimeSpan.FromSeconds(2), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "leaky_ntp_queued";

            await limiter.TryAcquireAsync(key, cost: 1, ct);
            ValueTask<RateLimitDecision> queuedTask = limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.False(queuedTask.IsCompleted);

            // Act
            timeProvider.WallClockOffset = TimeSpan.FromHours(-1);
            fakeTime.Advance(TimeSpan.FromSeconds(1));
            RateLimitDecision queuedResult = await queuedTask;

            // Assert
            Assert.True(queuedResult.IsAllowed);
        }
    }
}