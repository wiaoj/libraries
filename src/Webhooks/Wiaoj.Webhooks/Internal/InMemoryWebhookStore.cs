using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Thread-safe, high-performance in-memory implementation of <see cref="IWebhookStore"/> backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
internal sealed class InMemoryWebhookStore : IWebhookStore {
    private readonly ConcurrentDictionary<WebhookJobId, WebhookJobRecord> _jobs = new(WebhookJobId.OrdinalComparer);
    private readonly ConcurrentDictionary<WebhookEndpointId, List<WebhookJobId>> _endpointIndex = new(WebhookEndpointId.OrdinalComparer);
    private readonly Lock _lock = new();

    /// <inheritdoc/>
    public Task SaveAsync(WebhookJobRecord job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);

        this._jobs[job.Id] = job;

        lock(this._lock) {
            List<WebhookJobId> list = this._endpointIndex.GetOrAdd(job.EndpointId, static _ => []);
            list.Add(job.Id);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<WebhookJobRecord?> GetJobAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        this._jobs.TryGetValue(jobId, out WebhookJobRecord? job);
        return Task.FromResult(job);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetHistoryByEndpointAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        List<WebhookJobRecord> result = [];

        lock(this._lock) {
            if(this._endpointIndex.TryGetValue(endpointId, out List<WebhookJobId>? jobIds)) {
                foreach(WebhookJobId id in jobIds) {
                    if(this._jobs.TryGetValue(id, out WebhookJobRecord? job)) {
                        result.Add(job);
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>(result);
    }

    /// <inheritdoc/>
    public Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, CancellationToken cancellationToken = default) {
        if(this._jobs.TryGetValue(jobId, out WebhookJobRecord? job)) {
            job.Status = status;
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> TryClaimLeaseAsync(WebhookJobId jobId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(instanceId);
        Preca.ThrowIfNegative(duration);

        if(!this._jobs.TryGetValue(jobId, out WebhookJobRecord? job)) {
            return Task.FromResult(false);
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;

        lock(job) {
            // Already claimed by another active instance?
            if(job.LockedBy is not null && job.LockExpiresAt.HasValue && job.LockExpiresAt.Value > now && job.LockedBy != instanceId) {
                return Task.FromResult(false);
            }

            job.LockedBy = instanceId;
            job.LockExpiresAt = now.Add(duration);
            job.Status = WebhookJobStatus.InFlight;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc/>
    public Task RecordAttemptAsync(WebhookJobId jobId, WebhookDeliveryAttempt attempt, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(attempt);

        if(this._jobs.TryGetValue(jobId, out WebhookJobRecord? job)) {
            lock(job) {
                job.AddAttempt(attempt);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetStaleInFlightJobsAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default) {
        List<WebhookJobRecord> stale = [];

        foreach(KeyValuePair<WebhookJobId, WebhookJobRecord> kvp in this._jobs) {
            WebhookJobRecord job = kvp.Value;
            if(job.Status == WebhookJobStatus.InFlight && job.LockExpiresAt.HasValue && job.LockExpiresAt.Value < threshold) {
                stale.Add(job);
                if(stale.Count >= maxCount) {
                    break;
                }
            }
        }

        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>(stale);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);
        List<WebhookJobRecord> deadLettered = [];

        foreach(KeyValuePair<WebhookJobId, WebhookJobRecord> kvp in this._jobs) {
            if(kvp.Value.Status == WebhookJobStatus.DeadLettered) {
                deadLettered.Add(kvp.Value);
                if(deadLettered.Count >= maxCount) {
                    break;
                }
            }
        }

        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>(deadLettered);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(WebhookEndpointId endpointId, int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);
        List<WebhookJobRecord> deadLettered = [];

        lock(this._lock) {
            if(this._endpointIndex.TryGetValue(endpointId, out List<WebhookJobId>? jobIds)) {
                foreach(WebhookJobId id in jobIds) {
                    if(this._jobs.TryGetValue(id, out WebhookJobRecord? job) && job.Status == WebhookJobStatus.DeadLettered) {
                        deadLettered.Add(job);
                        if(deadLettered.Count >= maxCount) {
                            break;
                        }
                    }
                }
            }
        }

        return Task.FromResult<IReadOnlyList<WebhookJobRecord>>(deadLettered);
    }
}
