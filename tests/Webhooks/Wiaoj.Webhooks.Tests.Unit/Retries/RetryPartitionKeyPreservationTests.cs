using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Retries;

public sealed class RetryPartitionKeyPreservationTests {
    [Fact]
    public async Task InvokeAsync_WhenTransientFailureOccurs_MustPreserveOriginalCustomPartitionKey() {
        // Arrange
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 3 });
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        const string customPartitionKey = "order-aggregate-999";
        WebhookEndpointId endpointId = new("crm-endpoint");

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(
            endpoint: WebhookTestFactory.CreateEndpoint(endpointId),
            partitionKey: new WebhookPartitionKey(customPartitionKey));

        WebhookDelegate next = (ctx, ct) => {
            ctx.SetResult(WebhookDeliveryResult.Transient("503 Service Unavailable", 503));
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert: Re-enqueued job must preserve original custom partition key
        Assert.Single(transport.EnqueuedJobs);
        WebhookDeliveryJob reEnqueuedJob = transport.EnqueuedJobs[0].Job;
        Assert.Equal(customPartitionKey, reEnqueuedJob.PartitionKey.Value);
    }

    [Fact]
    public async Task InvokeAsync_WhenPartitionKeyIsDefaultEndpointId_PreservesEndpointIdAsPartitionKey() {
        // Arrange
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 3 });
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        WebhookEndpointId endpointId = new("default-endpoint");
        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(
            endpoint: WebhookTestFactory.CreateEndpoint(endpointId),
            partitionKey: WebhookPartitionKey.From(endpointId));

        WebhookDelegate next = (ctx, ct) => {
            ctx.SetResult(WebhookDeliveryResult.Transient("504 Gateway Timeout", 504));
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert: Partition key matches endpoint id
        Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(endpointId.Value, transport.EnqueuedJobs[0].Job.PartitionKey.Value);
    }

    [Fact]
    public async Task InvokeAsync_WhenTransientFailureHasRetryAfterHeader_PreservesPartitionKeyAndCustomDelay() {
        // Arrange
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 5 });
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        const string customPartitionKey = "tenant-shard-42";
        TimeSpan explicitRetryAfter = TimeSpan.FromSeconds(45);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(
            partitionKey: new WebhookPartitionKey(customPartitionKey));

        WebhookDelegate next = (ctx, ct) => {
            ctx.SetResult(WebhookDeliveryResult.Transient("429 Too Many Requests", 429, explicitRetryAfter));
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert: Preserves partition key and uses Retry-After delay
        Assert.Single(transport.EnqueuedJobs);
        (WebhookDeliveryJob reEnqueuedJob, TimeSpan? delay) = transport.EnqueuedJobs[0];

        Assert.Equal(customPartitionKey, reEnqueuedJob.PartitionKey.Value);
        Assert.Equal(explicitRetryAfter, delay);
    }

    [Fact]
    public async Task InvokeAsync_AcrossMultipleRetryAttempts_PreservesPartitionKeyContinuously() {
        // Arrange
        FakeWebhookTransport transport = new();
        ExponentialBackoffPolicy policy = new(new ExponentialBackoffOptions { MaxAttempts = 5 });
        RetryMiddleware middleware = new(policy, transport, NullLogger<RetryMiddleware>.Instance);

        const string customPartitionKey = "persistent-order-key";

        // Simulating attempt #2 with existing history
        List<WebhookDeliveryAttempt> history = [
            WebhookTestFactory.CreateAttempt(1, WebhookTestFactory.CreateTransientFailureResult("500", 500))
        ];

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext(
            partitionKey: new WebhookPartitionKey(customPartitionKey),
            attemptHistory: history);

        WebhookDelegate next = (ctx, ct) => {
            ctx.SetResult(WebhookDeliveryResult.Transient("500 Internal Server Error", 500));
            return Task.CompletedTask;
        };

        // Act
        await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

        // Assert: Preserves partition key on second failure
        Assert.Single(transport.EnqueuedJobs);
        Assert.Equal(customPartitionKey, transport.EnqueuedJobs[0].Job.PartitionKey.Value);
    }
}