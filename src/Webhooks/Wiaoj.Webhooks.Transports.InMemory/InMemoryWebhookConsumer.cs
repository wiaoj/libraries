using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Transports.InMemory.Diagnostics;

namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// Background service that runs a multi-worker concurrent consumer pool to continuously dequeue jobs from an
/// <see cref="InMemoryWebhookTransport"/> and process them with <see cref="IWebhookJobHandler"/>.
/// </summary>
public sealed class InMemoryWebhookConsumer : BackgroundService {
    private readonly InMemoryWebhookTransport _transport;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InMemoryWebhookTransportOptions _options;
    private readonly ILogger<InMemoryWebhookConsumer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookConsumer"/> class with options.
    /// </summary>
    /// <param name="transport">The in-memory transport to read jobs from.</param>
    /// <param name="scopeFactory">The factory used to create per-job DI scopes.</param>
    /// <param name="options">The transport configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryWebhookConsumer(
        InMemoryWebhookTransport transport,
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

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookConsumer"/> class with default options.
    /// </summary>
    /// <param name="transport">The in-memory transport to read jobs from.</param>
    /// <param name="scopeFactory">The factory used to create per-job DI scopes.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryWebhookConsumer(
        InMemoryWebhookTransport transport,
        IServiceScopeFactory scopeFactory,
        ILogger<InMemoryWebhookConsumer> logger)
        : this(transport, scopeFactory, Microsoft.Extensions.Options.Options.Create(new InMemoryWebhookTransportOptions()), logger) {
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        int workerCount = this._options.Concurrency;
        this._logger.LogConsumerStarted(workerCount);

        Task[] workerTasks = new Task[workerCount];

        for(int i = 0; i < workerCount; i++) {
            int workerId = i + 1;
            workerTasks[i] = Task.Run(() => WorkerLoopAsync(workerId, stoppingToken), CancellationToken.None);
        }

        try {
            await Task.WhenAll(workerTasks);
        }
        finally {
            this._logger.LogConsumerStopping();
        }
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken stoppingToken) {
        while(!stoppingToken.IsCancellationRequested) {
            WebhookDeliveryJob job;
            try {
                job = await this._transport.Reader.ReadAsync(stoppingToken);
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
                await handler.HandleAsync(job, stoppingToken);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(Exception ex) {
                // A job-level failure should never take the worker loop down.
                this._logger.LogConsumerJobError(ex, workerId, job.Id.Value, job.EndpointId.Value);
            }
        }
    }
}
