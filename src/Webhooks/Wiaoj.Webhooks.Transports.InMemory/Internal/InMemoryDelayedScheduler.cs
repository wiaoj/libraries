using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace Wiaoj.Webhooks.Transports.InMemory.Internal;

/// <summary>
/// High-performance, lock-free in-memory delayed job scheduler that orders jobs by monotonic timestamp.
/// Confines the priority queue to a single consumer loop driven by a lock-free channel inbox.
/// Monotonic timing ensures immunity against system wall-clock skew and NTP corrections.
/// </summary>
internal sealed class InMemoryDelayedScheduler : IAsyncDisposable, IDisposable {
    private readonly ChannelWriter<WebhookDeliveryJob> _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    // Lock-free MPSC (Multi-Producer Single-Consumer) Inbox
    private readonly Channel<ScheduledJobItem> _inbox = Channel.CreateUnbounded<ScheduledJobItem>(
        new UnboundedChannelOptions {
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    // Confined exclusively to the single consumer thread - NO locks needed!
    private readonly PriorityQueue<ScheduledJobItem, MonotonicTimestamp> _queue = new();

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
    /// Lock-free operation: posts directly to the internal concurrent channel.
    /// </summary>
    public void Schedule(WebhookDeliveryJob job, TimeSpan delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(InMemoryDelayedScheduler));

        MonotonicTimestamp dueTimestamp = this._timeProvider.GetMonotonicTimestamp().Add(delay);

        this._logger.LogJobScheduledDelayed(job.Id.Value, job.EndpointId.Value, delay.TotalMilliseconds);

        if(!this._inbox.Writer.TryWrite(new ScheduledJobItem(job, dueTimestamp, cancellationToken))) {
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(InMemoryDelayedScheduler));
        }
    }

    private async Task ProcessQueueAsync() {
        CancellationToken ct = this._cts.Token;
        ChannelReader<ScheduledJobItem> reader = this._inbox.Reader;

        while(!ct.IsCancellationRequested) {
            // 1. Drain all pending incoming jobs from the lock-free inbox into the priority queue
            while(reader.TryRead(out ScheduledJobItem incoming)) {
                this._queue.Enqueue(incoming, incoming.DueTimestamp);
            }

            // 2. Check the earliest scheduled job
            if(this._queue.TryPeek(out ScheduledJobItem nextItem, out MonotonicTimestamp dueTimestamp)) {
                MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();

                // If due time has passed, dequeue and flush immediately
                if(dueTimestamp <= now) {
                    this._queue.Dequeue();

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

                // 3. Queue has items, but not yet due: wait for EITHER a new incoming job OR the timer to expire
                TimeSpan waitDuration = dueTimestamp - now;

                using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                using ITimer timer = this._timeProvider.CreateTimer(
                    static state => ((CancellationTokenSource)state!).Cancel(),
                    delayCts,
                    waitDuration,
                    Timeout.InfiniteTimeSpan);

                try {
                    // Wakes up if:
                    // a) A new job is written to inbox (WaitToReadAsync returns true)
                    // b) Timer fires and cancels delayCts (OperationCanceledException)
                    // c) Application is shutting down (ct is canceled)
                    if(!await reader.WaitToReadAsync(delayCts.Token).ConfigureAwait(false)) {
                        break; // Channel completed
                    }
                }
                catch(OperationCanceledException) when(!ct.IsCancellationRequested) {
                    // Timer expired cleanly, loop will dequeue the expired item
                }
                catch(OperationCanceledException) {
                    break; // System shutdown
                }
            }
            else {
                // 4. Queue is completely empty: wait indefinitely for the next job to arrive
                try {
                    if(!await reader.WaitToReadAsync(ct).ConfigureAwait(false)) {
                        break; // Channel completed
                    }
                }
                catch(OperationCanceledException) {
                    break;
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
            this._inbox.Writer.TryComplete();
            this._cts.Cancel();
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
            this._inbox.Writer.TryComplete();
            await this._cts.CancelAsync().ConfigureAwait(false);

            try {
                await this._processingTask.ConfigureAwait(false);
            }
            catch(OperationCanceledException) { }

            this._cts.Dispose();
        }
        finally {
            this._disposeState.SetDisposed();
        }
    }

    private readonly record struct ScheduledJobItem(
        WebhookDeliveryJob Job,
        MonotonicTimestamp DueTimestamp,
        CancellationToken CancellationToken);
}