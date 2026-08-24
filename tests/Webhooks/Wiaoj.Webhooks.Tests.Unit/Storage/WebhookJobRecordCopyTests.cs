using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Storage;

[Trait("Category", "Unit")]
[Trait("Feature", "Copyable")]
[Trait("Component", "JobRecord")]
public sealed class WebhookJobRecordCopyTests {

    [Fact]
    public void CopyFrom_SynchronizesMutableExecutionStateAndAttempts() {
        // Arrange
        WebhookJobId jobId = WebhookJobId.NewJobId();
        WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId();
        DateTimeOffset now = DateTimeOffset.UtcNow;

        WebhookJobRecord target = new(jobId, endpointId, "order.created", "{}", now) {
            Status = WebhookJobStatus.Queued
        };

        WebhookJobRecord source = new(jobId, endpointId, "order.created", "{}", now) {
            Status = WebhookJobStatus.Retrying,
            NextAttemptAt = now.AddMinutes(5),
            LockedBy = "worker-pod-42",
            LockExpiresAt = now.AddMinutes(2)
        };
        source.AddAttempt(WebhookTestFactory.CreateAttempt(1));

        // Act: target copies mutable state from source
        target.CopyFrom(source);

        // Assert
        Assert.Equal(WebhookJobStatus.Retrying, target.Status);
        Assert.Equal(source.NextAttemptAt, target.NextAttemptAt);
        Assert.Equal("worker-pod-42", target.LockedBy);
        Assert.Equal(source.LockExpiresAt, target.LockExpiresAt);
        Assert.Single(target.Attempts);
        Assert.Equal(1, target.Attempts[0].AttemptNumber);
    }

    [Fact]
    public void CopyFrom_ThrowsInvalidOperationException_WhenJobIdsDoNotMatch() {
        // Arrange
        WebhookJobRecord target = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "order.created", "{}", DateTimeOffset.UtcNow);
        WebhookJobRecord source = new(WebhookJobId.NewJobId(), WebhookTestFactory.CreateEndpointId(), "order.created", "{}", DateTimeOffset.UtcNow);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => target.CopyFrom(source));
    }
}