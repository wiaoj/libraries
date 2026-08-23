using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using Wiaoj.Webhooks.Concurrency;
using Wiaoj.Webhooks.Diagnostics;

#pragma warning disable IDE0130 // Namespace matches root framework convention
namespace Wiaoj.Webhooks;
#pragma warning restore IDE0130

/// <summary>
/// Webhook delivery middleware that partitions and serializes outbound deliveries per <see cref="WebhookEndpointId"/>
/// using an injected <see cref="IWebhookDeliveryLock"/> to guarantee strict FIFO delivery order per endpoint.
/// </summary>
/// <remarks>
/// <para>
/// <b>Thread-Safety &amp; Ordering:</b> By wrapping downstream pipeline execution in an endpoint-specific lock,
/// concurrent dispatches targeting the same endpoint are executed sequentially, preventing race conditions
/// (e.g. out-of-order execution of dependent events such as <c>order.created</c> followed by <c>order.cancelled</c>).
/// </para>
/// <para>
/// <b>Zero-Allocation Diagnostics:</b> Measures lock acquisition wait times and execution duration using
/// high-precision timestamp math (<see cref="Stopwatch.GetTimestamp"/> and <see cref="Stopwatch.GetElapsedTime(long)"/>)
/// with zero heap allocations.
/// </para>
/// </remarks>
public sealed class PartitionedDeliveryMiddleware : IWebhookMiddleware {
    private readonly IWebhookDeliveryLock _deliveryLock;
    private readonly PartitionedDeliveryOptions _options;
    private readonly ILogger<PartitionedDeliveryMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PartitionedDeliveryMiddleware"/> class.
    /// </summary>
    /// <param name="deliveryLock">The synchronization lock provider used to serialize deliveries.</param>
    /// <param name="options">The configuration options controlling partition key selection.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is <see langword="null"/>.</exception>
    public PartitionedDeliveryMiddleware(
        IWebhookDeliveryLock deliveryLock,
        PartitionedDeliveryOptions options,
        ILogger<PartitionedDeliveryMiddleware> logger) {
        Preca.ThrowIfNull(deliveryLock);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._deliveryLock = deliveryLock;
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc/>
    public async Task InvokeAsync(WebhookDeliveryContext context, WebhookDelegate next, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNull(next);

        string partitionKey = this._options.PartitionKeySelector(context);

        long waitStartTimestamp = Stopwatch.GetTimestamp();

        // 1. Asynchronously acquire partition lock for this specific endpoint
        using IDisposable lockHandle = await this._deliveryLock.AcquireLockAsync(partitionKey, cancellationToken).ConfigureAwait(false);


        double lockWaitDurationMs = Stopwatch.GetElapsedTime(waitStartTimestamp).TotalMilliseconds;
        if(lockWaitDurationMs > 500) {
            this._logger.LogLockContention(context.Endpoint.Id, lockWaitDurationMs);
        }

        // 2. Execute downstream pipeline under exclusive endpoint lock
        await next(context, cancellationToken).ConfigureAwait(false);
    }
}