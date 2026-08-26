namespace Wiaoj.RateLimiting.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Feature", "FailOpen")]
public sealed class ResilientRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullInnerAlgorithm_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new ResilientRateLimiter(null!));
        }
    }

    public sealed class TheHealthyDelegation {

        [Fact]
        public async Task WhenInnerAllows_PropagatesAllowedDecision() {
            // Arrange
            MockAlgorithm mock = new(RateLimitDecision.Allowed(remaining: 15));
            ResilientRateLimiter resilient = new(mock);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await resilient.TryAcquireAsync("healthy_key", cost: 1, ct);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Equal(15, decision.Remaining);
            Assert.Equal(1, mock.CallCount);
        }

        [Fact]
        public async Task WhenInnerDenies_PropagatesDeniedDecision() {
            // Arrange
            MockAlgorithm mock = new(RateLimitDecision.Denied(TimeSpan.FromSeconds(30), remaining: 0));
            ResilientRateLimiter resilient = new(mock);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            RateLimitDecision decision = await resilient.TryAcquireAsync("denied_key", cost: 1, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(TimeSpan.FromSeconds(30), decision.RetryAfter);
            Assert.Equal(1, mock.CallCount);
        }
    }

    public sealed class TheFailOpenBehavior {

        [Fact]
        public async Task WhenStorageThrowsException_FailsOpenAndAllowsRequest() {
            // Arrange: Simulate storage failure (e.g. Redis socket timeout)
            FailingAlgorithm failing = new(new TimeoutException("Redis connection timed out."));
            ResilientRateLimiter resilient = new(failing);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Must not throw!
            RateLimitDecision decision = await resilient.TryAcquireAsync("failing_storage_key", cost: 1, ct);

            // Assert: Gracefully allowed through
            Assert.True(decision.IsAllowed);
            Assert.Null(decision.Remaining);
            Assert.Null(decision.RetryAfter);
        }

        [Fact]
        public async Task WhenCallerCancels_NeverSwallowsCancellationException() {
            // Arrange: Simulate caller-side cancellation
            FailingAlgorithm cancelling = new(new OperationCanceledException());
            ResilientRateLimiter resilient = new(cancelling);

            // Act & Assert: Must rethrow OperationCanceledException!
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                resilient.TryAcquireAsync("cancel_key", cost: 1, CancellationToken.None).AsTask());
        }
    }

    private sealed class MockAlgorithm(RateLimitDecision outcome) : IRateLimitAlgorithm {
        private int _callCount;
        public int CallCount => Volatile.Read(ref this._callCount);

        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this._callCount);
            return ValueTask.FromResult(outcome);
        }
    }

    private sealed class FailingAlgorithm(Exception exceptionToThrow) : IRateLimitAlgorithm {
        public ValueTask<RateLimitDecision> TryAcquireAsync(string key, int cost, CancellationToken cancellationToken = default) {
            throw exceptionToThrow;
        }
    }
}