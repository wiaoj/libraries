using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Transports.InMemory.Diagnostics;
using Wiaoj.Webhooks.Transports.InMemory.Internal;

namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// High-throughput, non-blocking in-process <see cref="IWebhookTransport"/> backed by <see cref="Channel{T}"/> and a background timer scheduler.
/// </summary>
public sealed class InMemoryWebhookTransport : IWebhookTransport, IDisposable {
    private readonly Channel<WebhookDeliveryJob> _channel;
    private readonly InMemoryDelayedScheduler _delayedScheduler;
    private readonly ILogger<InMemoryWebhookTransport> _logger;

    /// <summary>
    /// Initializes a new unbounded instance of the <see cref="InMemoryWebhookTransport"/> class.
    /// </summary>
    public InMemoryWebhookTransport() : this(new InMemoryWebhookTransportOptions(), NullLogger<InMemoryWebhookTransport>.Instance) {
    }

    /// <summary>
    /// Initializes a new bounded instance of the <see cref="InMemoryWebhookTransport"/> class with the specified capacity.
    /// </summary>
    /// <param name="capacity">The maximum number of unprocessed jobs the channel will buffer.</param>
    public InMemoryWebhookTransport(int capacity) : this(new InMemoryWebhookTransportOptions { Capacity = capacity }, NullLogger<InMemoryWebhookTransport>.Instance) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookTransport"/> class with configured options.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    public InMemoryWebhookTransport(InMemoryWebhookTransportOptions options) : this(options, NullLogger<InMemoryWebhookTransport>.Instance) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookTransport"/> class with configured options and logger.
    /// </summary>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryWebhookTransport(InMemoryWebhookTransportOptions options, ILogger<InMemoryWebhookTransport> logger) {
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._logger = logger;

        if(options.Capacity.HasValue) {
            this._channel = Channel.CreateBounded<WebhookDeliveryJob>(new BoundedChannelOptions(options.Capacity.Value) {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            });
        }
        else {
            this._channel = Channel.CreateUnbounded<WebhookDeliveryJob>(new UnboundedChannelOptions {
                SingleReader = false,
                SingleWriter = false
            });
        }

        this._delayedScheduler = new InMemoryDelayedScheduler(this._channel.Writer, TimeProvider.System, logger);
    }

    /// <summary>
    /// Gets the reader side of the channel, used by <see cref="InMemoryWebhookConsumer"/> to dequeue jobs.
    /// </summary>
    internal ChannelReader<WebhookDeliveryJob> Reader => this._channel.Reader;

    /// <summary>
    /// Gets the writer side of the channel.
    /// </summary>
    internal ChannelWriter<WebhookDeliveryJob> Writer => this._channel.Writer;

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) =>
        EnqueueAsync(job, null, cancellationToken);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) =>
        EnqueueAsync(job, null, CancellationToken.None);

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) =>
        EnqueueAsync(job, delay, CancellationToken.None);

    /// <inheritdoc/>
    public async Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);

        if(delay.HasValue && delay.Value > TimeSpan.Zero) {
            // Non-blocking background timer scheduling: returns immediately to the caller in 0ms!
            this._delayedScheduler.Schedule(job, delay.Value, cancellationToken);
            return;
        }

        this._logger.LogJobEnqueuingImmediate(job.Id.Value, job.EndpointId.Value);
        await this._channel.Writer.WriteAsync(job, cancellationToken);
    }

    /// <inheritdoc/>
    public void Dispose() {
        this._delayedScheduler.Dispose();
        this._channel.Writer.TryComplete();
    }
}