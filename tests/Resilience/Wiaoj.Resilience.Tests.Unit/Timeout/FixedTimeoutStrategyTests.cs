using Microsoft.Extensions.Time.Testing;
using DotNetTimeout = System.Threading.Timeout;

namespace Wiaoj.Resilience.Tests.Unit.Timeout;

[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
[Trait("Feature", "Timeout")]
public sealed class FixedTimeoutStrategyTests {

    public sealed class TheConstructorValidation {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeTimeout_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            // Arrange & Act & Assert
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new FixedTimeoutStrategy(TimeSpan.FromTicks(invalidTicks)));
        }

        [Fact]
        public void GivenNullTimeProvider_ThrowsArgumentNullException() {
            // Arrange & Act & Assert
            Assert.ThrowsAny<ArgumentNullException>(() => new FixedTimeoutStrategy(TimeSpan.FromSeconds(1), null!));
        }
    }

    public sealed class TheExecutionBoundary {

        [Fact]
        public async Task ExecuteAsync_WhenOperationCompletesWithinDeadline_ReturnsResultSuccessfully() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(5), timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            string result = await strategy.ExecuteAsync(
                "fast_operation",
                async token => {
                    await Task.Yield();
                    return "success";
                },
                ct);

            // Assert
            Assert.Equal("success", result);
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationExceedsDeadline_ThrowsTimeoutException() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(2), timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act & Assert
            Task executionTask = strategy.ExecuteAsync(
                "slow_operation",
                async token => {
                    await Task.Delay(DotNetTimeout.InfiniteTimeSpan, token);
                    return true;
                },
                ct).AsTask();

            timeProvider.Advance(TimeSpan.FromSeconds(3));

            await Assert.ThrowsAsync<TimeoutException>(() => executionTask);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCallerCancelsBeforeDeadline_ThrowsOperationCanceledException() {
            // Arrange
            FakeTimeProvider timeProvider = new();
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(10), timeProvider);
            using CancellationTokenSource callerCts = new();

            // Act & Assert
            Task executionTask = strategy.ExecuteAsync(
                "cancelled_operation",
                async token => {
                    await Task.Delay(DotNetTimeout.InfiniteTimeSpan, token);
                    return true;
                },
                callerCts.Token).AsTask();

            await callerCts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executionTask);
        }
    }
}