namespace Wiaoj.RateLimiting.Tests.Unit.Abstractions;

public sealed class RateLimitDecisionTests {
    [Fact]
    public void Allowed_CreatesValidAllowedDecision() {
        RateLimitDecision decision = RateLimitDecision.Allowed(remaining: 5);

        Assert.True(decision.IsAllowed);
        Assert.Null(decision.RetryAfter);
        Assert.Equal(5, decision.Remaining);
    }

    [Fact]
    public void Denied_WithValidRetryAfter_CreatesValidDeniedDecision() {
        TimeSpan retryAfter = TimeSpan.FromSeconds(3);
        RateLimitDecision decision = RateLimitDecision.Denied(retryAfter, remaining: 0);

        Assert.False(decision.IsAllowed);
        Assert.Equal(retryAfter, decision.RetryAfter);
        Assert.Equal(0, decision.Remaining);
    }

    [Fact]
    public void Denied_WithZeroRetryAfter_IsAllowed() {
        RateLimitDecision decision = RateLimitDecision.Denied(TimeSpan.Zero);

        Assert.False(decision.IsAllowed);
        Assert.Equal(TimeSpan.Zero, decision.RetryAfter);
    }

    [Fact]
    public void Denied_WithNegativeRetryAfter_ThrowsArgumentOutOfRangeException() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(
            () => RateLimitDecision.Denied(TimeSpan.FromSeconds(-1)));
    }
}