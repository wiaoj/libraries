using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Wiaoj.Webhooks.Transports.InMemory.Internal;

/// <summary>
/// In-memory delayed job scheduler that orders jobs by due time using a priority queue and flushes expired jobs to a channel.
/// Uses <see cref="DisposeState"/> to coordinate safe synchronous and asynchronous disposal.
/// </summary>
internal sealed class InMemoryDelayedScheduler : IAsyncDisposable, IDisposable {
    private readonly ChannelWriter<WebhookDeliveryJob> _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly PriorityQueue<ScheduledJobItem, DateTimeOffset> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly Lock _lock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processingTask;
    private readonly DisposeState _disposeState = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDelayedScheduler"/> class.
    /// </summary>
    public InMemoryDelayedScheduler(
        ChannelWriter<WebhookDeliveryJob> writer,
        TimeProvider timeProvider,
        ILogger logger) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);

        this._writer = writer;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Schedules a delivery job to be enqueued when its delay window expires, observing the cancellation token.
    /// </summary>
    public void Schedule(WebhookDeliveryJob job, TimeSpan delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(InMemoryDelayedScheduler));

        DateTimeOffset dueTime = this._timeProvider.GetUtcNow().Add(delay);

        lock(this._lock) {
            if(this._disposeState.IsDisposingOrDisposed) {
                return;
            }

            this._logger.LogJobScheduledDelayed(job.Id.Value, job.EndpointId.Value, delay.TotalMilliseconds);
            this._queue.Enqueue(new ScheduledJobItem(job, cancellationToken), dueTime);
        }

        lock(this._lock) {
            if(this._signal.CurrentCount == 0) {
                this._signal.Release();
            }
        }
    }

    private async Task ProcessQueueAsync() {
        CancellationToken ct = this._cts.Token;

        while(!ct.IsCancellationRequested) {
            ScheduledJobItem nextItem = default;
            bool hasJob = false;
            TimeSpan waitDuration = Timeout.InfiniteTimeSpan;

            lock(this._lock) {
                if(this._queue.TryPeek(out _, out DateTimeOffset dueTime)) {
                    DateTimeOffset now = this._timeProvider.GetUtcNow();
                    if(dueTime <= now) {
                        nextItem = this._queue.Dequeue();
                        hasJob = true;
                    }
                    else {
                        waitDuration = dueTime - now;
                    }
                }
            }

            if(hasJob) {
                if(nextItem.CancellationToken.IsCancellationRequested) {
                    this._logger.LogDelayedJobCancelled(nextItem.Job.Id.Value, nextItem.Job.EndpointId.Value);
                    continue;
                }

                try {
                    await this._writer.WriteAsync(nextItem.Job, ct).ConfigureAwait(false);
                    this._logger.LogDelayedJobFlushed(nextItem.Job.Id.Value, nextItem.Job.EndpointId.Value);
                }
                catch(ChannelClosedException) {
                    break;
                }
                catch(OperationCanceledException) {
                    break;
                }

                continue;
            }

            if(waitDuration == Timeout.InfiniteTimeSpan) {
                try {
                    await this._signal.WaitAsync(ct).ConfigureAwait(false);
                }
                catch(OperationCanceledException) {
                    break;
                }
            }
            else {
                using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Task signalTask = this._signal.WaitAsync(delayCts.Token);
                Task delayTask = Task.Delay(waitDuration, this._timeProvider, delayCts.Token);

                Task completed = await Task.WhenAny(signalTask, delayTask).ConfigureAwait(false);
                if(completed != signalTask) {
                    delayCts.Cancel();
                }
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        if(!this._disposeState.TryBeginDispose()) {
            return;
        }

        try {
            this._cts.Cancel();
            this._signal.Dispose();
            this._cts.Dispose();
        }
        finally {
            this._disposeState.SetDisposed();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() {
        if(!this._disposeState.TryBeginDispose()) {
            await this._disposeState.WaitForDisposedAsync().ConfigureAwait(false);
            return;
        }

        try {
            await this._cts.CancelAsync().ConfigureAwait(false);

            try {
                await this._processingTask.ConfigureAwait(false);
            }
            catch(OperationCanceledException) { }

            this._signal.Dispose();
            this._cts.Dispose();
        }
        finally {
            this._disposeState.SetDisposed();
        }
    }

    private readonly record struct ScheduledJobItem(WebhookDeliveryJob Job, CancellationToken CancellationToken);
}