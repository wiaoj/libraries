using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Serializes the handling of each job within this process, so two copies of a job on the same instance — for example a
/// delayed retry and a copy re-enqueued by recovery — are never delivered concurrently. Leases already separate instances,
/// but they are held per instance, so they cannot separate two copies within one.
/// </summary>
/// <remarks>
/// A second copy waits for the first rather than being dropped: a retry scheduled with no delay can reach a worker before
/// the attempt that scheduled it has finished, and it must still run. Once the first copy finishes, the store decides:
/// a finished job can no longer be leased.
/// </remarks>
internal sealed class WebhookJobExecutionGuard {
    private readonly ConcurrentDictionary<WebhookJobId, TaskCompletionSource> _running = new();

    /// <summary>Waits until no other copy of <paramref name="jobId"/> is being handled in this process, then takes the slot.</summary>
    /// <returns>A handle that releases the slot when disposed.</returns>
    public async ValueTask<Releaser> EnterAsync(WebhookJobId jobId, CancellationToken cancellationToken) {
        TaskCompletionSource mine = new(TaskCreationOptions.RunContinuationsAsynchronously);

        while(true) {
            TaskCompletionSource current = this._running.GetOrAdd(jobId, mine);
            if(ReferenceEquals(current, mine)) {
                return new Releaser(this, jobId, mine);
            }

            await current.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Releases a slot taken by <see cref="EnterAsync"/>.</summary>
    internal readonly struct Releaser(WebhookJobExecutionGuard guard, WebhookJobId jobId, TaskCompletionSource slot) : IDisposable {
        public void Dispose() {
            guard._running.TryRemove(new KeyValuePair<WebhookJobId, TaskCompletionSource>(jobId, slot));
            slot.TrySetResult();
        }
    }
}
