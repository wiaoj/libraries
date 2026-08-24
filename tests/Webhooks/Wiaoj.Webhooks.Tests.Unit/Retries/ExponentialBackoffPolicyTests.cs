using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

public sealed class ExponentialBackoffPolicyTests {
    [Fact]
    public void ShouldRetry_ReturnsFalse_WhenLastResultIsSuccess() {
        ExponentialBackoffPolicy policy = new();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDeliveryResult success = WebhookTestFactory.CreateSuccessResult(200);

        bool shouldRetry = policy.ShouldRetry(context, success, out TimeSpan nextDelay);

        Assert.False(shouldRetry);
        Assert.Equal(TimeSpan.Zero, nextDelay);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalse_WhenErrorIsNonTransient() {
        ExponentialBackoffPolicy policy = new();
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
         
        WebhookDeliveryResult nonTransientFailure = WebhookTestFactory.CreatePermanentFailureResult("Not Found", 404);

        bool shouldRetry = policy.ShouldRetry(context, nonTransientFailure, out TimeSpan nextDelay);

        Assert.False(shouldRetry);
        Assert.Equal(TimeSpan.Zero, nextDelay);
    }

    [Fact]
    public void ShouldRetry_ReturnsFalse_WhenMaxAttemptsReached() {
        ExponentialBackoffOptions options = new() { MaxAttempts = 3 };
        ExponentialBackoffPolicy policy = new(options);

        // Simulate 2 previous attempts already recorded in history (attempt #3 is currently running and failing)
        List<WebhookDeliveryAttempt> history = [
            WebhookTestFactory.CreateAttempt(1, WebhookTestFactory.CreateFailureResult("err1", 500)),
            WebhookTestFactory.CreateAttempt(2, WebhookTestFactory.CreateFailureResult("err2", 500))
        ];
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(history);
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("err3", 500);

        bool shouldRetry = policy.ShouldRetry(context, failure, out TimeSpan nextDelay);

        Assert.False(shouldRetry);
        Assert.Equal(TimeSpan.Zero, nextDelay);
    }

    [Fact]
    public void ShouldRetry_CalculatesExponentialDelays_WhenJitterDisabled() {
        ExponentialBackoffOptions options = new() {
            MaxAttempts = 5,
            InitialDelay = TimeSpan.FromSeconds(2),
            Multiplier = 2.0,
            Jitter = null,
            MaxDelay = TimeSpan.FromMinutes(10)
        };
        ExponentialBackoffPolicy policy = new(options);
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("Service Unavailable", 503);

        // Attempt 1 (history count = 0): factor = 2^0 = 1 -> delay = 2s
        WebhookDeliveryContext context1 = WebhookTestFactory.CreateContext();
        Assert.True(policy.ShouldRetry(context1, failure, out TimeSpan delay1));
        Assert.Equal(TimeSpan.FromSeconds(2), delay1);

        // Attempt 2 (history count = 1): factor = 2^1 = 2 -> delay = 4s
        WebhookDeliveryContext context2 = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure)
        ]);
        Assert.True(policy.ShouldRetry(context2, failure, out TimeSpan delay2));
        Assert.Equal(TimeSpan.FromSeconds(4), delay2);

        // Attempt 3 (history count = 2): factor = 2^2 = 4 -> delay = 8s
        WebhookDeliveryContext context3 = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure),
            WebhookTestFactory.CreateAttempt(2, failure)
        ]);
        Assert.True(policy.ShouldRetry(context3, failure, out TimeSpan delay3));
        Assert.Equal(TimeSpan.FromSeconds(8), delay3);
    }

    [Fact]
    public void ShouldRetry_CapsDelayAtMaxDelay() {
        ExponentialBackoffOptions options = new() {
            MaxAttempts = 10,
            InitialDelay = TimeSpan.FromSeconds(10),
            Multiplier = 10.0,
            Jitter = null,
            MaxDelay = TimeSpan.FromSeconds(30)
        };
        ExponentialBackoffPolicy policy = new(options);
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("timeout", 504);

        // Attempt 3 (history count = 2): 10 * 10^2 = 1000s -> capped at 30s
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure),
            WebhookTestFactory.CreateAttempt(2, failure)
        ]);

        Assert.True(policy.ShouldRetry(context, failure, out TimeSpan delay));
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void ShouldRetry_WithJitter_ReturnsBoundedNonZeroDelay() {
        // Arrange: 5 saniye baz gecikme ve %10 (Jitter.Medium) jitter
        ExponentialBackoffOptions options = new() {
            MaxAttempts = 4,
            InitialDelay = TimeSpan.FromSeconds(5),
            Jitter = Wiaoj.Extensions.Jitter.Medium // +/- %10
        };
        ExponentialBackoffPolicy policy = new(options);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("500", 500);

        // Act
        bool shouldRetry = policy.ShouldRetry(context, failure, out TimeSpan delay);

        // Assert: 5s ± %10 yani [4.5s, 5.5s] aralığında olmalı
        Assert.True(shouldRetry);
        Assert.True(delay >= TimeSpan.FromSeconds(4.5), $"Delay was {delay.TotalSeconds}s, expected >= 4.5s");
        Assert.True(delay <= TimeSpan.FromSeconds(5.5), $"Delay was {delay.TotalSeconds}s, expected <= 5.5s");
    }

    [Fact]
    public void ShouldRetry_WithoutJitter_ReturnsExactDeterministicDelay() {
        // Arrange: Jitter tamamen kapatıldığında
        ExponentialBackoffOptions options = new() {
            MaxAttempts = 4,
            InitialDelay = TimeSpan.FromSeconds(5),
            Jitter = null // Jitter kapalı
        };
        ExponentialBackoffPolicy policy = new(options);
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("500", 500);

        // Act
        bool shouldRetry = policy.ShouldRetry(context, failure, out TimeSpan delay);

        // Assert: Tam olarak 5 saniye dönmeli
        Assert.True(shouldRetry);
        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    [Fact]
    public void Options_Validate_Throws_OnInvalidParameters() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffOptions { MaxAttempts = 0 }.Validate());

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffOptions { Multiplier = 0.5 }.Validate());

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffOptions { InitialDelay = TimeSpan.FromSeconds(-1) }.Validate());

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffOptions { InitialDelay = TimeSpan.FromMinutes(5), MaxDelay = TimeSpan.FromMinutes(1) }.Validate());
    }
}
