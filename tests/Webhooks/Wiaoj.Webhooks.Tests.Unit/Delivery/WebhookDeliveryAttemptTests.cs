using Wiaoj.Primitives;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Delivery;

public sealed class WebhookDeliveryAttemptTests {
    [Fact]
    public void Constructor_SetsAllProperties_WhenValid() {
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        UnixTimestamp attemptedAt = UnixTimestamp.Now;
        TimeSpan duration = TimeSpan.FromMilliseconds(250);
        WebhookDeliveryResult result = WebhookTestFactory.CreateSuccessResult();

        WebhookDeliveryAttempt attempt = new(endpointId, 1, attemptedAt, duration, result);

        Assert.Equal(endpointId, attempt.EndpointId);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(attemptedAt, attempt.AttemptedAt);
        Assert.Equal(duration, attempt.Duration);
        Assert.Same(result, attempt.Result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsWhenAttemptNumberIsLessThanOne(int attemptNumber) {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            WebhookTestFactory.CreateAttempt(attemptNumber));
    }

    [Fact]
    public void Constructor_ThrowsWhenDurationIsNegative() {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() =>
            WebhookTestFactory.CreateAttempt(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Constructor_ThrowsWhenResultIsNull() {
        Assert.ThrowsAny<ArgumentNullException>(() =>
            new WebhookDeliveryAttempt(WebhookTestFactory.CreateEndpointId(), 1, UnixTimestamp.Now, TimeSpan.Zero, null!));
    }

    [Fact]
    public void Constructor_AllowsZeroDuration() {
        WebhookDeliveryAttempt attempt = WebhookTestFactory.CreateAttempt(TimeSpan.Zero);

        Assert.Equal(TimeSpan.Zero, attempt.Duration);
    }

    [Fact]
    public void IsSuccess_ReturnsTrue_WhenResultIsSuccess() {
        WebhookDeliveryAttempt attempt = WebhookTestFactory.CreateAttempt(WebhookTestFactory.CreateSuccessResult());

        Assert.True(attempt.IsSuccess);
    }

    [Fact]
    public void IsSuccess_ReturnsFalse_WhenResultIsFailure() {
        WebhookDeliveryAttempt attempt = WebhookTestFactory.CreateAttempt(WebhookTestFactory.CreateFailureResult());

        Assert.False(attempt.IsSuccess);
    }
}