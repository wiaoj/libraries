using Wiaoj.Concurrency;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// High-performance in-memory implementation of <see cref="IWebhookDeliveryLock"/> backed by a 4096-stripe non-blocking lock.
/// </summary>
internal sealed class StripedWebhookDeliveryLock : IWebhookDeliveryLock {
    private readonly StripedLock<WebhookEndpointId> _stripedLock;

    /// <summary>
    /// Initializes a new instance with the default 4096 stripes.
    /// </summary>
    public StripedWebhookDeliveryLock() : this(4096) {
    }

    /// <summary>
    /// Initializes a new instance with the specified number of power-of-two stripes.
    /// </summary>
    public StripedWebhookDeliveryLock(int stripes) {
        this._stripedLock = new StripedLock<WebhookEndpointId>(stripes);
    }

    /// <inheritdoc/>
    public async ValueTask<IDisposable> AcquireLockAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        return await this._stripedLock.LockAsync(endpointId, cancellationToken).ConfigureAwait(false);
    }
}