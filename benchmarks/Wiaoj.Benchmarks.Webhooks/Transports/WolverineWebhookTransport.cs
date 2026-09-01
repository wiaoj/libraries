using Wiaoj.Preconditions;
using Wiaoj.Webhooks;
using Wolverine;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

public static class WolverineWebhookJobHandler {
    public static async Task Handle(WebhookDeliveryJob job, IWebhookJobHandler jobHandler, CancellationToken ct) {
        await jobHandler.HandleAsync(job, ct).ConfigureAwait(false);
        BenchmarkCompletionTracker.SignalItemCompleted();
    }
}

public sealed class WolverineWebhookTransport(IMessageBus bus) : IWebhookTransport {
    private readonly IMessageBus _bus = bus;

    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        return this._bus.PublishAsync(job).AsTask();
    }

    public Task EnqueueAsync(WebhookDeliveryJob job) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => EnqueueAsync(job, CancellationToken.None);
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) => EnqueueAsync(job, cancellationToken);

    public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
        throw new NotImplementedException();
    }
}