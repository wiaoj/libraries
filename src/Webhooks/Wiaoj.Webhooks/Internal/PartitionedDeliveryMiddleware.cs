namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Webhook delivery middleware that partitions and serializes outbound deliveries per <see cref="WebhookEndpointId"/>
/// using an injected <see cref="IWebhookDeliveryLock"/> to guarantee strict FIFO delivery order per endpoint.
/// </summary>
public sealed class PartitionedDeliveryMiddleware : IWebhookMiddleware {
    private readonly IWebhookDeliveryLock _deliveryLock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionedDeliveryMiddleware"/> class.
    /// </summary>
    /// <param name="deliveryLock">The synchronization lock provider used to serialize deliveries.</param>
    public PartitionedDeliveryMiddleware(IWebhookDeliveryLock deliveryLock) {
        Preca.ThrowIfNull(deliveryLock);
        this._deliveryLock = deliveryLock;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        using(await this._deliveryLock.AcquireLockAsync(context.Endpoint.Id, cancellationToken).ConfigureAwait(false)) {
            await next(context, cancellationToken).ConfigureAwait(false);
        }
    }
}