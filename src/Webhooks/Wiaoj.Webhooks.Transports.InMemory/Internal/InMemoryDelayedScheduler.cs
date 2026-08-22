using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Transports.InMemory.Diagnostics;

namespace Wiaoj.Webhooks.Transports.InMemory.Internal;

/// <summary>
/// Non-blocking, in-memory delayed execution scheduler that buffers delayed jobs using <see cref="TimeProvider"/> timers
/// and flushes them into the target channel writer when their delay expires.
/// </summary>
internal sealed class InMemoryDelayedScheduler : IDisposable {
    private readonly ChannelWriter<WebhookDeliveryJob> _writer;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;
    private readonly Lock _lock = new();
    private readonly List<ITimer> _activeTimers = [];
    private bool _isDisposed;

    public InMemoryDelayedScheduler(
        ChannelWriter<WebhookDeliveryJob> writer,
        TimeProvider? timeProvider = null,
        ILogger? logger = null) {
        Preca.ThrowIfNull(writer);
        this._writer = writer;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Schedules a job to be written to the channel writer after the specified delay without blocking the caller.
    /// </summary>
    /// <param name="job">The job to enqueue.</param>
    /// <param name="delay">The delay duration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void Schedule(WebhookDeliveryJob job, TimeSpan delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);

        lock(this._lock) {
            if(this._isDisposed) {
                return;
            }

            this._logger.LogJobScheduledDelayed(job.Id.Value, job.EndpointId.Value, delay.TotalMilliseconds);

            ITimer? timer = null;
            timer = this._timeProvider.CreateTimer(
                callback: _ => {
                    // Remove timer from tracking list
                    lock(this._lock) {
                        if(timer is not null) {
                            this._activeTimers.Remove(timer);
                            timer.Dispose();
                        }
                    }

                    if(!cancellationToken.IsCancellationRequested) {
                        this._logger.LogDelayedJobFlushed(job.Id.Value, job.EndpointId.Value);
                        this._writer.TryWrite(job);
                    }
                },
                state: null,
                dueTime: delay,
                period: Timeout.InfiniteTimeSpan
            );

            this._activeTimers.Add(timer);

            if(cancellationToken.CanBeCanceled) {
                cancellationToken.Register(() => {
                    lock(this._lock) {
                        this._activeTimers.Remove(timer);
                        timer.Dispose();
                    }
                    this._logger.LogDelayedJobCancelled(job.Id.Value, job.EndpointId.Value);
                });
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose() {
        lock(this._lock) {
            if(this._isDisposed) {
                return;
            }
            this._isDisposed = true;

            foreach(ITimer timer in this._activeTimers) {
                timer.Dispose();
            }
            this._activeTimers.Clear();
        }
    }
}
