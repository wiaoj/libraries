namespace Wiaoj.Webhooks;

/// <summary>
/// Defines a contract for synchronizing and serializing outbound webhook deliveries per endpoint.
/// Supports both single-instance in-memory striping and multi-instance distributed cluster locks.
/// </summary>
public interface IWebhookDeliveryLock {
    /// <summary>
    /// Asynchronously acquires an execution lock for the specified endpoint.
    /// </summary>
    /// <param name="endpointId">The endpoint identifier to lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable handle that releases the lock upon disposal.</returns>
    ValueTask<IDisposable> AcquireLockAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously acquires an execution lock for the specified partition key (e.g. EndpointId, OrderId, TenantId).
    /// </summary>
    /// <param name="partitionKey">The partition identifier to lock.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A disposable handle that releases the partition lock upon disposal.</returns>
    ValueTask<IDisposable> AcquireLockAsync(string partitionKey, CancellationToken cancellationToken = default);
}