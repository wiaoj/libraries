namespace Wiaoj.RateLimiting.Tests.Unit.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
[Trait("Feature", "RateLimitDecision")]
public sealed class RateLimitDecisionTests {

    public sealed class TheAllowedFactories {

        [Fact]
        public void Allowed_WithoutParameters_InitializesWithAllowedTrueAndNullMetadata() {
            // Act
            RateLimitDecision decision = RateLimitDecision.Allowed();

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Null(decision.RetryAfter);
            Assert.Null(decision.Remaining);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(long.MaxValue)]
        public void Allowed_WithRemaining_InitializesWithExactRemainingValue(long expectedRemaining) {
            // Act
            RateLimitDecision decision = RateLimitDecision.Allowed(expectedRemaining);

            // Assert
            Assert.True(decision.IsAllowed);
            Assert.Null(decision.RetryAfter);
            Assert.Equal(expectedRemaining, decision.Remaining);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Allowed_WithNegativeRemaining_ThrowsArgumentOutOfRangeException(long invalidRemaining) {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => RateLimitDecision.Allowed(invalidRemaining));
        }
    }

    public sealed class TheDeniedFactories {

        [Fact]
        public void Denied_WithRetryAfterOnly_SetsAllowedFalseAndDefaultsRemainingToZero() {
            // Arrange
            TimeSpan retryAfter = TimeSpan.FromSeconds(30);

            // Act
            RateLimitDecision decision = RateLimitDecision.Denied(retryAfter);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(retryAfter, decision.RetryAfter);
            Assert.Equal(0, decision.Remaining);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(50)]
        public void Denied_WithRetryAfterAndRemaining_SetsExactRemainingCapacity(long remainingCapacity) {
            // Arrange
            TimeSpan retryAfter = TimeSpan.FromMinutes(1);

            // Act
            RateLimitDecision decision = RateLimitDecision.Denied(retryAfter, remainingCapacity);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(retryAfter, decision.RetryAfter);
            Assert.Equal(remainingCapacity, decision.Remaining);
        }

        [Fact]
        public void Denied_WithZeroTimeSpan_PreservesZeroDuration() {
            // Arrange & Act
            RateLimitDecision decision = RateLimitDecision.Denied(TimeSpan.Zero);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
            Assert.Equal(0, decision.Remaining);
        }

        [Fact]
        public void Denied_WithNegativeRetryAfter_ThrowsArgumentOutOfRangeException() {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(
                () => RateLimitDecision.Denied(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void Denied_WithNegativeRemaining_ThrowsArgumentOutOfRangeException() {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(
                () => RateLimitDecision.Denied(TimeSpan.FromSeconds(10), remaining: -1));
        }
    }

    public sealed class TheRecordStructSemanticsAndEquality {

        [Fact]
        public void DefaultStruct_HasDefaultFieldValues() {
            // Arrange
            RateLimitDecision defaultDecision = default;

            // Assert
            Assert.False(defaultDecision.IsAllowed);
            Assert.Null(defaultDecision.RetryAfter);
            Assert.Null(defaultDecision.Remaining);
        }

        [Fact]
        public void InstancesWithIdenticalValues_AreEqualAndHaveMatchingHashCodes() {
            // Arrange
            RateLimitDecision d1 = RateLimitDecision.Denied(TimeSpan.FromSeconds(15), remaining: 2);
            RateLimitDecision d2 = RateLimitDecision.Denied(TimeSpan.FromSeconds(15), remaining: 2);

            // Assert
            Assert.Equal(d1, d2);
            Assert.True(d1 == d2);
            Assert.False(d1 != d2);
            Assert.Equal(d1.GetHashCode(), d2.GetHashCode());
        }

        [Fact]
        public void InstancesWithDifferentValues_AreNotEqual() {
            // Arrange
            RateLimitDecision allowed1 = RateLimitDecision.Allowed(5);
            RateLimitDecision allowed2 = RateLimitDecision.Allowed(10);
            RateLimitDecision denied = RateLimitDecision.Denied(TimeSpan.FromSeconds(5), 5);

            // Assert
            Assert.NotEqual(allowed1, allowed2);
            Assert.NotEqual(allowed1, denied);
            Assert.True(allowed1 != allowed2);
            Assert.True(allowed1 != denied);
        }
    }

    public sealed class ThePatternMatchingAndDeconstruction {

        [Fact]
        public void PatternMatching_CorrectlyIdentifiesAllowedAndDeniedStates() {
            // Arrange
            RateLimitDecision allowed = RateLimitDecision.Allowed(10);
            RateLimitDecision denied = RateLimitDecision.Denied(TimeSpan.FromSeconds(5));

            // Act
            bool isAllowedRecognized = allowed is { IsAllowed: true, Remaining: > 0 };
            bool isDeniedRecognized = denied is { IsAllowed: false, RetryAfter: not null };

            // Assert
            Assert.True(isAllowedRecognized);
            Assert.True(isDeniedRecognized);
        }

        [Fact]
        public void PositionalDeconstruction_ExtractsAllPropertiesCorrectly() {
            // Arrange
            TimeSpan expectedRetry = TimeSpan.FromSeconds(45);
            const long expectedRemaining = 3;
            RateLimitDecision decision = RateLimitDecision.Denied(expectedRetry, expectedRemaining);

            // Act: Deconstruct via record struct positional matching
            (bool isAllowed, TimeSpan? retryAfter, long? remaining) = decision;

            // Assert
            Assert.False(isAllowed);
            Assert.Equal(expectedRetry, retryAfter);
            Assert.Equal(expectedRemaining, remaining);
        }
    }
}