using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wiaoj.Webhooks.Diagnostics;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Resilient background service that periodically prunes finished webhook jobs whose retention window
/// has expired, keeping the jobs and delivery attempts tables bounded.
/// </summary>
/// <remarks>
/// Only <see cref="WebhookJobStatus.Delivered"/> and <see cref="WebhookJobStatus.DeadLettered"/> jobs are ever
/// deleted, each measured from <see cref="WebhookJobRecord.CompletedAt"/>; running jobs are never touched.
/// </remarks>
internal sealed class WebhookRetentionCleanerService : BackgroundService {
    private readonly IWebhookStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly WebhookRetentionOptions _options;
    private readonly ILogger<WebhookRetentionCleanerService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookRetentionCleanerService"/> class.
    /// </summary>
    public WebhookRetentionCleanerService(
        IWebhookStore store,
        TimeProvider timeProvider,
        IOptions<WebhookRetentionOptions> retentionOptions,
        ILogger<WebhookRetentionCleanerService> logger) {

        Preca.ThrowIfNull(store);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(retentionOptions);
        Preca.ThrowIfNull(logger);

        retentionOptions.Value.Validate();

        this._store = store;
        this._timeProvider = timeProvider;
        this._options = retentionOptions.Value;
        this._logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        using PeriodicTimer timer = new(this._options.PollingInterval, this._timeProvider);

        while(!stoppingToken.IsCancellationRequested) {
            try {
                await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                await SweepAsync(stoppingToken).ConfigureAwait(false);
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) {
                break;
            }
            catch(Exception ex) {
                this._logger.LogRetentionSweepFailed(ex);
            }
        }
    }

    /// <summary>
    /// Executes a single retention sweep, draining expired jobs in batches until the backlog is gone.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of jobs deleted by this sweep.</returns>
    public async Task<int> SweepAsync(CancellationToken cancellationToken = default) {
        long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        DateTimeOffset now = this._timeProvider.GetUtcNow();
        DateTimeOffset deliveredBefore = now.Subtract(this._options.DeliveredRetention);
        DateTimeOffset deadLetteredBefore = now.Subtract(this._options.DeadLetterRetention);

        this._logger.LogRetentionSweepStarting(now);

        int totalDeleted = 0;

        // A full batch means the backlog may hold more; keep each transaction short and drain until it does not.
        int deleted;
        do {
            deleted = await this._store.PruneJobsAsync(
                deliveredBefore,
                deadLetteredBefore,
                this._options.BatchSize,
                cancellationToken).ConfigureAwait(false);
            totalDeleted += deleted;
        } while(deleted > 0 && deleted >= this._options.BatchSize && !cancellationToken.IsCancellationRequested);

        double durationMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        WebhookMeter.RetentionSweepDuration.Record(durationMs);

        if(totalDeleted > 0) {
            WebhookMeter.PrunedJobsCount.Add(totalDeleted);
            this._logger.LogRetentionSweepCompleted(totalDeleted);
        }

        return totalDeleted;
    }
}
