namespace Wiaoj.Webhooks;

/// <summary>
/// Queues a <see cref="WebhookDeliveryJob"/> for asynchronous processing by whatever backend
/// implements this contract (an in-memory channel, Postgres outbox, a RabbitMq/Kafka consumer, etc.).
/// </summary>
public interface IWebhookTransport {
    /// <summary>
    /// Enqueues a webhook delivery job for immediate processing.
    /// </summary>
    /// <param name="job">The delivery job to enqueue.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> that completes once the job has been accepted by the transport.</returns>
    Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a webhook delivery job for immediate processing.
    /// </summary>
    /// <param name="job">The delivery job to enqueue.</param>
    /// <returns>A <see cref="Task"/> that completes once the job has been accepted by the transport.</returns>
    Task EnqueueAsync(WebhookDeliveryJob job);

    /// <summary>
    /// Enqueues a webhook delivery job for processing, optionally delaying its visibility to consumers.
    /// </summary>
    /// <param name="job">The delivery job to enqueue.</param>
    /// <param name="delay">
    /// The amount of time to wait before the job becomes available for processing.
    /// <see langword="null"/> or <see cref="TimeSpan.Zero"/> means the job is available immediately.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A <see cref="Task"/> that completes once the job has been accepted by the transport.</returns>
    Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken);

    /// <summary>
    /// Enqueues a webhook delivery job for processing, optionally delaying its visibility to consumers.
    /// </summary>
    /// <param name="job">The delivery job to enqueue.</param>
    /// <param name="delay">
    /// The amount of time to wait before the job becomes available for processing.
    /// <see langword="null"/> or <see cref="TimeSpan.Zero"/> means the job is available immediately.
    /// </param>
    /// <returns>A <see cref="Task"/> that completes once the job has been accepted by the transport.</returns>
    Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay);

    /// <summary>
    /// Enqueues a batch of webhook delivery jobs for asynchronous processing.
    /// </summary>
    /// <param name="jobs">The batch of delivery jobs to enqueue.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous enqueue operation.</returns>
    Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default);
}