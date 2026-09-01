using MassTransit;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

public sealed class MassTransitWebhookJobConsumer(IWebhookJobHandler jobHandler) : IConsumer<WebhookDeliveryJob> {
    public async Task Consume(ConsumeContext<WebhookDeliveryJob> context) {
        await jobHandler.HandleAsync(context.Message, context.CancellationToken).ConfigureAwait(false);
        BenchmarkCompletionTracker.SignalItemCompleted();
    }
}

public sealed class MassTransitWebhookTransport(IBus bus) : IWebhookTransport {
    private readonly IBus _bus = bus;

    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        return this._bus.Publish(job, cancellationToken);
    }

    public Task EnqueueAsync(WebhookDeliveryJob job) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) => EnqueueAsync(job, cancellationToken);

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}