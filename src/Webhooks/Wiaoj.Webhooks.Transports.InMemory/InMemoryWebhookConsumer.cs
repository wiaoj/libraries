using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// Background service that runs a concurrent consumer worker pool to continuously dequeue and process jobs from
/// <see cref="InMemoryWebhookTransport"/> or <see cref="ShardedWebhookTransport"/>.
/// </summary>
public sealed class InMemoryWebhookConsumer : BackgroundService {
    private readonly IWebhookTransport _transport;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryWebhookTransportOptions _options;
    private readonly ILogger<InMemoryWebhookConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookConsumer"/> class with all required dependencies.
    /// </summary>
    /// <param name="transport">The transport instance to read jobs from.</param>
    /// <param name="scopeFactory">The factory used to create per-job DI scopes.</param>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is <see langword="null"/>.</exception>
    public InMemoryWebhookConsumer(
        IWebhookTransport transport,
        IServiceScopeFactory scopeFactory,
        IOptions<InMemoryWebhookTransportOptions> options,
        ILogger<InMemoryWebhookConsumer> logger) {
        Preca.ThrowIfNull(transport);
        Preca.ThrowIfNull(scopeFactory);
        Preca.ThrowIfNull(options);
        Preca.ThrowIfNull(logger);

        this._transport = transport;
        this._scopeFactory = scopeFactory;
        this._options = options.Value;
        this._logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        if(this._transport is ShardedWebhookTransport sharded) {
            await ExecuteShardedAsync(sharded, stoppingToken).ConfigureAwait(false);
        }
        else if(this._transport is InMemoryWebhookTransport single) {
            await ExecuteSingleAsync(single, stoppingToken).ConfigureAwait(false);
        }
        else {
            throw new InvalidOperationException($"Unsupported transport type '{this._transport.GetType().FullName}' for InMemoryWebhookConsumer.");
        }
    }

    private async Task ExecuteShardedAsync(ShardedWebhookTransport sharded, CancellationToken stoppingToken) {
        int shardCount = sharded.ShardCount;
        this._logger.LogConsumerStarted(shardCount);

        Task[] workerTasks = new Task[shardCount];

        for(int i = 0; i < shardCount; i++) {
            int workerId = i + 1;
            if(sharded.GetShard(i) is InMemoryWebhookTransport shardTransport) {
                workerTasks[i] = Task.Run(() => WorkerLoopAsync(workerId, shardTransport.Reader, stoppingToken), CancellationToken.None);
            }
        }

        try {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        finally {
            this._logger.LogConsumerStopping();
        }
    }

    private async Task ExecuteSingleAsync(InMemoryWebhookTransport single, CancellationToken stoppingToken) {
        int workerCount = this._options.Concurrency;
        this._logger.LogConsumerStarted(workerCount);

        Task[] workerTasks = new Task[workerCount];

        for(int i = 0; i < workerCount; i++) {
            int workerId = i + 1;
            workerTasks[i] = Task.Run(() => WorkerLoopAsync(workerId, single.Reader, stoppingToken), CancellationToken.None);
        }

        try {
            await Task.WhenAll(workerTasks).ConfigureAwait(false);
        }
        finally {
            this._logger.LogConsumerStopping();
        }
    }

    private async Task WorkerLoopAsync(int workerId, ChannelReader<WebhookDeliveryJob> reader, CancellationToken stoppingToken) {
        while(!stoppingToken.IsCancellationRequested) {
            WebhookDeliveryJob job;
            try {
                job = await reader.ReadAsync(stoppingToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(ChannelClosedException) {
                break;
            }

            this._logger.LogWorkerDequeuedJob(workerId, job.Id.Value, job.EndpointId.Value);

            try {
                await using AsyncServiceScope scope = this._scopeFactory.CreateAsyncScope();
                IWebhookJobHandler handler = scope.ServiceProvider.GetRequiredService<IWebhookJobHandler>();
                await handler.HandleAsync(job, stoppingToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(Exception ex) {
                this._logger.LogConsumerJobError(ex, workerId, job.Id.Value, job.EndpointId.Value);
            }
        }
    }
}