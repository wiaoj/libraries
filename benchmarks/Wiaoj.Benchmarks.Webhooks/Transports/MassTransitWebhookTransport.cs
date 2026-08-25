using MassTransit;
using Wiaoj.Preconditions;
using Wiaoj.Webhooks;

namespace Wiaoj.Benchmarks.Webhooks.Transports;

/// <summary>
/// Webhook transport implementation backed by MassTransit in-memory bus.
/// </summary>
public sealed class MassTransitWebhookTransport : IWebhookTransport {
    private readonly IBus _bus;

    /// <summary>
    /// Initializes a new instance of the <see cref="MassTransitWebhookTransport"/> class.
    /// </summary>
    /// <param name="bus">The MassTransit bus instance.</param>
    public MassTransitWebhookTransport(IBus bus) {
        Preca.ThrowIfNull(bus);
        this._bus = bus;
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);
        Interlocked.Increment(ref ProcessedCounters.SentMassTransit);
        return this._bus.Publish(job, cancellationToken);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) => EnqueueAsync(job, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => EnqueueAsync(job, delay, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        Interlocked.Increment(ref ProcessedCounters.SentMassTransit);

        return delay.HasValue && delay.Value > TimeSpan.Zero
            ? this._bus.Publish(job, ctx => ctx.Delay = delay.Value, cancellationToken)
            : this._bus.Publish(job, cancellationToken);
    }
}