using Wiaoj.Webhooks.Transports.InMemory;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

public sealed class InMemoryWebhookTransportTests {
    [Fact]
    public async Task EnqueueAsync_MakesJobAvailable_ToReader() {
        InMemoryWebhookTransport transport = new();
        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();

        await transport.EnqueueAsync(job);

        bool read = transport.Reader.TryRead(out WebhookDeliveryJob? dequeued);
        Assert.True(read);
        Assert.Same(job, dequeued);
    }

    [Fact]
    public async Task EnqueueAsync_PreservesFifoOrder() {
        InMemoryWebhookTransport transport = new();
        WebhookDeliveryJob first = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("a"));
        WebhookDeliveryJob second = WebhookTestFactory.CreateJob(WebhookTestFactory.CreateEndpointId("b"));

        await transport.EnqueueAsync(first);
        await transport.EnqueueAsync(second);

        transport.Reader.TryRead(out WebhookDeliveryJob? firstOut);
        transport.Reader.TryRead(out WebhookDeliveryJob? secondOut);

        Assert.Same(first, firstOut);
        Assert.Same(second, secondOut);
    }

    [Fact]
    public async Task EnqueueAsync_ThrowsWhenJobIsNull() {
        InMemoryWebhookTransport transport = new();

        await Assert.ThrowsAnyAsync<ArgumentNullException>(() => transport.EnqueueAsync(null!));
    }

    [Fact]
    public async Task EnqueueAsync_ThrowsOperationCanceledException_WhenCancelledBeforeWrite() {
        InMemoryWebhookTransport transport = new(capacity: 1);
        WebhookDeliveryJob fillerJob = WebhookTestFactory.CreateJob();
        await transport.EnqueueAsync(fillerJob); // fill the bounded channel

        using CancellationTokenSource cts = new();
        cts.Cancel();

        WebhookDeliveryJob blockedJob = WebhookTestFactory.CreateJob();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => transport.EnqueueAsync(blockedJob, null, cts.Token));
    }

    [Fact]
    public async Task EnqueueAsync_DelaysEnqueue_NonBlocking_AndFlushesWhenTimerExpires() {
        InMemoryWebhookTransport transport = new();
        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();

        Task enqueueTask = transport.EnqueueAsync(job, TimeSpan.FromMilliseconds(50));

        // Returns immediately without blocking the caller (0ms return)
        Assert.True(enqueueTask.IsCompleted);

        // Immediately after calling, job should NOT be in the channel yet
        Assert.False(transport.Reader.TryRead(out _));

        // Wait for timer to fire
        await Task.Delay(100);

        bool read = transport.Reader.TryRead(out WebhookDeliveryJob? dequeued);
        Assert.True(read);
        Assert.Same(job, dequeued);
    }

    [Fact]
    public async Task EnqueueAsync_CancelsScheduledJob_WhenTokenIsCancelled() {
        InMemoryWebhookTransport transport = new();
        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();
        using CancellationTokenSource cts = new();

        await transport.EnqueueAsync(job, TimeSpan.FromMilliseconds(50), cts.Token);
        await cts.CancelAsync();

        await Task.Delay(100);

        // Job was cancelled before timer fired, so never written to channel
        Assert.False(transport.Reader.TryRead(out _));
    }

    [Fact]
    public async Task EnqueueAsync_ThroughInterface_CallsUnderlyingTransport() {
        IWebhookTransport transport = new InMemoryWebhookTransport();
        WebhookDeliveryJob job = WebhookTestFactory.CreateJob();

        await transport.EnqueueAsync(job);

        InMemoryWebhookTransport concrete = (InMemoryWebhookTransport)transport;
        bool read = concrete.Reader.TryRead(out WebhookDeliveryJob? dequeued);
        Assert.True(read);
        Assert.Same(job, dequeued);
    }
}