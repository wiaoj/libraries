using Microsoft.Extensions.Time.Testing;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms.LeakyBucket;

public sealed class LeakyBucketQueueRateLimiterTests {
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static (LeakyBucketQueueRateLimiter Sut, FakeTimeProvider Time) CreateSut(int capacity, TimeSpan period) {
        FakeTimeProvider time = new(Epoch);
        LeakyBucketQueueRateLimiter sut = new(capacity, period, time);
        return (sut, time);
    }

    // ---------------------------------------------------------------------
    // Positive cases & Queue/Shaping Behavior
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenQueueEmpty_CompletesSynchronouslyWithoutDelay() {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(5));

        ValueTask<RateLimitDecision> task = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        RateLimitDecision decision = await task;
        Assert.True(decision.IsAllowed);
        Assert.Equal(4, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenBacklogExists_WaitsUntilTurnBeforeAllowing() {
        // capacity = 5, period = 5s => 1 request / second emission
        (LeakyBucketQueueRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 5, period: TimeSpan.FromSeconds(5));

        // 1st request: executes immediately at t=0
        RateLimitDecision first = await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(first.IsAllowed);

        // 2nd request: needs to wait 1s (baseline is t=1s)
        ValueTask<RateLimitDecision> secondTask = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(secondTask.IsCompleted); // Still suspended / waiting

        // Advance time by 1 second — 2nd request should now complete
        time.Advance(TimeSpan.FromSeconds(1));

        RateLimitDecision second = await secondTask;
        Assert.True(second.IsAllowed);
    }

    [Fact]
    public async Task TryAcquireAsync_MultipleQueuedRequests_AreShapedSequentially() {
        (LeakyBucketQueueRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 3, period: TimeSpan.FromSeconds(3)); // 1s per item

        Task<RateLimitDecision> req1 = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        Task<RateLimitDecision> req2 = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();
        Task<RateLimitDecision> req3 = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken).AsTask();

        // req1 completes immediately (turn at t=0)
        Assert.True(req1.IsCompletedSuccessfully);
        Assert.False(req2.IsCompleted);
        Assert.False(req3.IsCompleted);

        // Advance 1s -> req2 completes (turn at t=1s)
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(req2.IsCompletedSuccessfully);
        Assert.False(req3.IsCompleted);

        // Advance 1s -> req3 completes (turn at t=2s)
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.True(req3.IsCompletedSuccessfully);

        RateLimitDecision[] results = await Task.WhenAll(req1, req2, req3);
        Assert.All(results, r => Assert.True(r.IsAllowed));
    }

    [Fact]
    public async Task TryAcquireAsync_DifferentKeys_QueueIndependently() {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 2, period: TimeSpan.FromSeconds(2));

        ValueTask<RateLimitDecision> keyA = sut.TryAcquireAsync("a", cancellationToken: TestContext.Current.CancellationToken);
        ValueTask<RateLimitDecision> keyB = sut.TryAcquireAsync("b", cancellationToken: TestContext.Current.CancellationToken);

        // Both keys are empty at start, so both complete immediately without waiting on each other
        Assert.True(keyA.IsCompletedSuccessfully);
        Assert.True(keyB.IsCompletedSuccessfully);
    }

    // ---------------------------------------------------------------------
    // Negative cases & Backlog Rejection
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenBacklogFull_RejectsImmediatelyWithoutWaiting() {
        // capacity = 2 over 2s. Backlog can hold at most 2 items.
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 2, period: TimeSpan.FromSeconds(2));

        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken); // t=0 (TAT=1s)
        _ = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);   // t=1s (TAT=2s, max backlog reached)

        // 3rd attempt: pushes backlog to 3s > maxBacklog(2s) => rejected immediately
        ValueTask<RateLimitDecision> overflow = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(overflow.IsCompletedSuccessfully); // Immediate rejection, caller does NOT wait
        RateLimitDecision denied = await overflow;
        Assert.False(denied.IsAllowed);
        Assert.NotNull(denied.RetryAfter);
        Assert.Equal(TimeSpan.FromSeconds(1), denied.RetryAfter.Value);
    }

    [Fact]
    public async Task TryAcquireAsync_CostExceedsCapacity_IsRejectedImmediately() {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 3, period: TimeSpan.FromSeconds(3));

        ValueTask<RateLimitDecision> task = sut.TryAcquireAsync("key", cost: 10, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(task.IsCompletedSuccessfully);
        RateLimitDecision decision = await task;
        Assert.False(decision.IsAllowed);
        Assert.Equal(TimeSpan.FromSeconds(3), decision.RetryAfter);
    }

    // ---------------------------------------------------------------------
    // Cancellation & TAT Rollback
    // ---------------------------------------------------------------------

    [Fact]
    public async Task TryAcquireAsync_WhenCancelledWhileWaiting_RollsBackReservationForSubsequentRequests() {
        (LeakyBucketQueueRateLimiter sut, FakeTimeProvider time) = CreateSut(capacity: 2, period: TimeSpan.FromSeconds(2));

        // 1st request takes the immediate slot
        await sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        // 2nd request queues with a cancellation token source
        using CancellationTokenSource cts = new();
        Task<RateLimitDecision> waitingReq = sut.TryAcquireAsync("key", cancellationToken: cts.Token).AsTask();

        Assert.False(waitingReq.IsCompleted);

        // Cancel the waiting request before its turn
        await cts.CancelAsync();
        await Assert.ThrowsAsync<TaskCanceledException>(async () => await waitingReq);

        // 3rd request: because 2nd request rolled back its TAT contribution upon cancellation,
        // this new request should fit within capacity instead of being rejected as backlog-full!
        ValueTask<RateLimitDecision> newReq = sut.TryAcquireAsync("key", cancellationToken: TestContext.Current.CancellationToken);

        // Advance time to allow the 3rd request to complete
        time.Advance(TimeSpan.FromSeconds(1));
        RateLimitDecision result = await newReq;

        Assert.True(result.IsAllowed);
    }

    // ---------------------------------------------------------------------
    // Argument validation
    // ---------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCapacity_Throws(int capacity) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketQueueRateLimiter(capacity, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Constructor_WithZeroOrNegativePeriod_Throws() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketQueueRateLimiter(1, TimeSpan.Zero));
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => new LeakyBucketQueueRateLimiter(1, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNullKey_ThrowsArgumentNullException() {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentNullException>(
            async () => await sut.TryAcquireAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TryAcquireAsync_WithEmptyKey_ThrowsArgumentException() {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentException>(
            async () => await sut.TryAcquireAsync(string.Empty, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TryAcquireAsync_WithNonPositiveCost_Throws(int cost) {
        (LeakyBucketQueueRateLimiter sut, _) = CreateSut(capacity: 1, period: TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
            async () => await sut.TryAcquireAsync("key", cost: cost, cancellationToken: TestContext.Current.CancellationToken));
    }
}