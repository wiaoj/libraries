namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

public sealed class ResilientRateLimiterTests {
    private sealed class ThrowingAlgorithm : IRateLimitAlgorithm {
        public Exception? ExceptionToThrow { get; set; }
        public RateLimitDecision DecisionToReturn { get; set; } = RateLimitDecision.Allowed(10);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost = 1, CancellationToken cancellationToken = default) {
            if(this.ExceptionToThrow is not null) {
                throw this.ExceptionToThrow;
            }
            return ValueTask.FromResult(this.DecisionToReturn);
        }
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInnerSucceeds_ReturnsInnerDecisionIntact() {
        ThrowingAlgorithm inner = new() {
            DecisionToReturn = RateLimitDecision.Allowed(remaining: 7)
        };
        ResilientRateLimiter sut = new(inner);

        RateLimitDecision decision = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal(7, decision.Remaining);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenInnerThrowsStorageException_ExecutesFailOpenAndAllowsRequest() {
        ThrowingAlgorithm inner = new() {
            ExceptionToThrow = new TimeoutException("Redis connection timed out.")
        };
        ResilientRateLimiter sut = new(inner);

        RateLimitDecision decision = await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken);

        // Fail-Open guarantees the request is allowed rather than crashing the user's API
        Assert.True(decision.IsAllowed);
        Assert.Null(decision.Remaining);
        Assert.Null(decision.RetryAfter);
    }

    [Fact]
    public async Task TryAcquireAsync_WhenCallerCancels_DoesNotSwallowOperationCanceledException() {
        ThrowingAlgorithm inner = new() {
            ExceptionToThrow = new OperationCanceledException()
        };
        ResilientRateLimiter sut = new(inner);

        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await sut.TryAcquireAsync("key1", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_WithNullInner_ThrowsArgumentNullException() {
        Assert.ThrowsAny<ArgumentNullException>(() => new ResilientRateLimiter(null!));
    }
}