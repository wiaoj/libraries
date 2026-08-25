using Wiaoj.Preconditions;
using Wiaoj.Webhooks;
using Wolverine;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

/// <summary>
/// Webhook transport implementation backed by Wolverine in-memory local queue.
/// </summary>
public sealed class WolverineWebhookTransport : IWebhookTransport {
    private readonly IMessageBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="WolverineWebhookTransport"/> class.
    /// </summary>
    /// <param name="bus">The Wolverine message bus instance.</param>
    public WolverineWebhookTransport(IMessageBus bus) {
        Preca.ThrowIfNull(bus);
        this._bus = bus;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);
        Interlocked.Increment(ref ProcessedCounters.SentWolverine);
        return this._bus.PublishAsync(job).AsTask();
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) => EnqueueAsync(job, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => EnqueueAsync(job, delay, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        Interlocked.Increment(ref ProcessedCounters.SentWolverine);

        return delay.HasValue && delay.Value > TimeSpan.Zero
            ? this._bus.PublishAsync(job, new DeliveryOptions { ScheduleDelay = delay.Value }).AsTask()
            : this._bus.PublishAsync(job).AsTask();
    }
}