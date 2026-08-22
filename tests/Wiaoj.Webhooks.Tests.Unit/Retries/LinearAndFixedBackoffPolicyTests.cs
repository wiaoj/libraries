using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

public sealed class LinearAndFixedBackoffPolicyTests {
    [Fact]
    public void LinearBackoff_CalculatesStepDelays_Correctly() {
        LinearBackoffPolicy policy = new(
            maxAttempts: 4,
            initialDelay: TimeSpan.FromSeconds(1),
            step: TimeSpan.FromSeconds(2),
            maxDelay: TimeSpan.FromSeconds(10));

        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("error", 500);

        // Attempt 1: 1s
        WebhookDeliveryContext ctx1 = WebhookTestFactory.CreateContext();
        Assert.True(policy.ShouldRetry(ctx1, failure, out TimeSpan delay1));
        Assert.Equal(TimeSpan.FromSeconds(1), delay1);

        // Attempt 2: 1 + 2 = 3s
        WebhookDeliveryContext ctx2 = WebhookTestFactory.CreateContext([WebhookTestFactory.CreateAttempt(1, failure)]);
        Assert.True(policy.ShouldRetry(ctx2, failure, out TimeSpan delay2));
        Assert.Equal(TimeSpan.FromSeconds(3), delay2);

        // Attempt 3: 1 + 4 = 5s
        WebhookDeliveryContext ctx3 = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure),
            WebhookTestFactory.CreateAttempt(2, failure)
        ]);
        Assert.True(policy.ShouldRetry(ctx3, failure, out TimeSpan delay3));
        Assert.Equal(TimeSpan.FromSeconds(5), delay3);

        // Attempt 4: Max attempts reached
        WebhookDeliveryContext ctx4 = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure),
            WebhookTestFactory.CreateAttempt(2, failure),
            WebhookTestFactory.CreateAttempt(3, failure)
        ]);
        Assert.False(policy.ShouldRetry(ctx4, failure, out _));
    }

    [Fact]
    public void FixedIntervalBackoff_ReturnsConstantDelay() {
        FixedIntervalBackoffPolicy policy = new(maxAttempts: 3, interval: TimeSpan.FromSeconds(5));
        WebhookDeliveryResult failure = WebhookTestFactory.CreateFailureResult("error", 500);

        WebhookDeliveryContext ctx1 = WebhookTestFactory.CreateContext();
        Assert.True(policy.ShouldRetry(ctx1, failure, out TimeSpan delay1));
        Assert.Equal(TimeSpan.FromSeconds(5), delay1);

        WebhookDeliveryContext ctx2 = WebhookTestFactory.CreateContext([WebhookTestFactory.CreateAttempt(1, failure)]);
        Assert.True(policy.ShouldRetry(ctx2, failure, out TimeSpan delay2));
        Assert.Equal(TimeSpan.FromSeconds(5), delay2);

        WebhookDeliveryContext ctx3 = WebhookTestFactory.CreateContext([
            WebhookTestFactory.CreateAttempt(1, failure),
            WebhookTestFactory.CreateAttempt(2, failure)
        ]);
        Assert.False(policy.ShouldRetry(ctx3, failure, out _));
    }

    [Fact]
    public void Constructors_Throw_WhenArgumentsAreInvalid() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new LinearBackoffPolicy(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new LinearBackoffPolicy(3, TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(10)));

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new FixedIntervalBackoffPolicy(0, TimeSpan.FromSeconds(5)));

        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            new FixedIntervalBackoffPolicy(3, TimeSpan.FromSeconds(-5)));
    }
}
