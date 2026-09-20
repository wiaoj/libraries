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

    private readonly int? _capacity;
    private readonly DelayedQueueOverflowPolicy _overflowPolicy;

    // Jobs admitted and not yet flushed, cancelled or dropped. Written by producers (Schedule) and the consumer loop,
    // so the queue can be bounded without the producers touching the queue itself.
    private int _pending;

    /// <summary>
    /// Initializes a new unbounded instance of the <see cref="InMemoryDelayedScheduler"/> class.
    /// </summary>
    public InMemoryDelayedScheduler(
        ChannelWriter<WebhookDeliveryJob> writer,
        TimeProvider timeProvider,
        ILogger logger)
        : this(writer, timeProvider, logger, null, DelayedQueueOverflowPolicy.PersistOnlyFallback) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDelayedScheduler"/> class.
    /// </summary>
    /// <param name="writer">The channel the scheduler flushes due jobs into.</param>
    /// <param name="timeProvider">The time source; monotonic, so wall-clock jumps don't move due times.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="maxCapacity">The maximum number of delayed jobs held, or <see langword="null"/> for unbounded.</param>
    /// <param name="overflowPolicy">What happens to a job scheduled while the queue is at <paramref name="maxCapacity"/>.</param>
    public InMemoryDelayedScheduler(
        ChannelWriter<WebhookDeliveryJob> writer,
        TimeProvider timeProvider,
        ILogger logger,
        int? maxCapacity,
        DelayedQueueOverflowPolicy overflowPolicy) {
        Preca.ThrowIfNull(writer);
        Preca.ThrowIfNull(timeProvider);
        Preca.ThrowIfNull(logger);
        if(maxCapacity.HasValue) {
            Preca.ThrowIfLessThan(maxCapacity.Value, 1);
        }

        this._writer = writer;
        this._timeProvider = timeProvider;
        this._logger = logger;
        this._capacity = maxCapacity;
        this._overflowPolicy = overflowPolicy;
        this._processingTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>Gets the number of jobs admitted and not yet flushed, cancelled or dropped.</summary>
    internal int PendingCount => Volatile.Read(ref this._pending);

    /// <summary>
    /// Schedules a delivery job to be enqueued when its delay window expires, observing the cancellation token.
    /// Lock-free operation: posts directly to the internal concurrent channel.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when the job was admitted; <see langword="false"/> when the queue is full and the policy
    /// is <see cref="DelayedQueueOverflowPolicy.PersistOnlyFallback"/>, leaving the job to the store and recovery.
    /// </returns>
    /// <exception cref="DelayedQueueFullException">
    /// The queue is full and the policy is <see cref="DelayedQueueOverflowPolicy.Reject"/>.
    /// </exception>
    public bool Schedule(WebhookDeliveryJob job, TimeSpan delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);
        this._disposeState.ThrowIfDisposingOrDisposed(nameof(InMemoryDelayedScheduler));

        if(!TryAdmit(job)) {
            return false;
        }

        MonotonicTimestamp dueTimestamp = this._timeProvider.GetMonotonicTimestamp().Add(delay);

        this._logger.LogJobScheduledDelayed(job.Id.Value, job.EndpointId.Value, delay.TotalMilliseconds);

        if(!this._inbox.Writer.TryWrite(new ScheduledJobItem(job, dueTimestamp, cancellationToken))) {
            Interlocked.Decrement(ref this._pending);
            this._disposeState.ThrowIfDisposingOrDisposed(nameof(InMemoryDelayedScheduler));
        }

        return true;
    }

    /// <summary>Takes a slot in the queue, applying the overflow policy when there is none left.</summary>
    private bool TryAdmit(WebhookDeliveryJob job) {
        int pending = Interlocked.Increment(ref this._pending);

        if(this._capacity is not int capacity || pending <= capacity) {
            return true;
        }

        // DropOldest admits the job and lets the consumer trim the least urgent ones, so an urgent retry is never
        // refused just because the queue is full of jobs due much later.
        if(this._overflowPolicy == DelayedQueueOverflowPolicy.DropOldest) {
            return true;
        }

        Interlocked.Decrement(ref this._pending);
        this._logger.LogDelayedQueueFull(job.Id.Value, job.EndpointId.Value, capacity, this._overflowPolicy.ToString());

        return this._overflowPolicy != DelayedQueueOverflowPolicy.Reject
            ? false
            : throw new DelayedQueueFullException(job.Id, capacity);
    }

    /// <summary>
    /// Drops the jobs due furthest in the future until the queue is back at capacity. Runs on the consumer loop, which
    /// owns the queue, and only under <see cref="DelayedQueueOverflowPolicy.DropOldest"/>.
    /// </summary>
    private void TrimToCapacity() {
        if(this._capacity is not int capacity || this._queue.Count <= capacity) {
            return;
        }

        // PriorityQueue can't remove its maximum, so the queue is rebuilt: drained, ordered by due time, and the most
        // urgent `capacity` items are put back. Only runs while over capacity.
        (ScheduledJobItem Item, MonotonicTimestamp Due)[] items = new (ScheduledJobItem, MonotonicTimestamp)[this._queue.Count];
        int index = 0;
        while(this._queue.TryDequeue(out ScheduledJobItem item, out MonotonicTimestamp due)) {
            items[index++] = (item, due);
        }

        Array.Sort(items, static (left, right) => left.Due.CompareTo(right.Due));

        for(int i = 0; i < capacity; i++) {
            this._queue.Enqueue(items[i].Item, items[i].Due);
        }

        for(int i = capacity; i < items.Length; i++) {
            WebhookDeliveryJob dropped = items[i].Item.Job;
            Interlocked.Decrement(ref this._pending);
            this._logger.LogDelayedQueueFull(
                dropped.Id.Value,
                dropped.EndpointId.Value,
                capacity,
                DelayedQueueOverflowPolicy.DropOldest.ToString());
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

            TrimToCapacity();

            // 2. Check the earliest scheduled job
            if(this._queue.TryPeek(out ScheduledJobItem nextItem, out MonotonicTimestamp dueTimestamp)) {
                MonotonicTimestamp now = this._timeProvider.GetMonotonicTimestamp();

                // If due time has passed, dequeue and flush immediately
                if(dueTimestamp <= now) {
                    this._queue.Dequeue();

                    if(nextItem.CancellationToken.IsCancellationRequested) {
                        Interlocked.Decrement(ref this._pending);
                        this._logger.LogDelayedJobCancelled(nextItem.Job.Id.Value, nextItem.Job.EndpointId.Value);
                        continue;
                    }

                    try {
                        await this._writer.WriteAsync(nextItem.Job, ct).ConfigureAwait(false);
                        Interlocked.Decrement(ref this._pending);
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

                // 3. Queue has items, but not yet due: wait for EITHER a new incoming job OR the due time to pass.
                //
                // The due-time source is a CancellationTokenSource driven by the TimeProvider, not a hand-made timer that
                // cancels a separate source: disposing that pair raced with its callback — Timer.Dispose does not wait for a
                // running callback, which then called Cancel on a disposed source and threw on the timer thread.
                using CancellationTokenSource dueCts = new(dueTimestamp - now, this._timeProvider);
                using CancellationTokenSource delayCts = CancellationTokenSource.CreateLinkedTokenSource(ct, dueCts.Token);

                // Time may have moved between reading 'now' and arming the wait — a clock that jumps, or a thread that was
                // descheduled. The wait is armed relative to the time it was armed at, so re-check before sleeping.
                if(dueTimestamp <= this._timeProvider.GetMonotonicTimestamp()) {
                    continue;
                }

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