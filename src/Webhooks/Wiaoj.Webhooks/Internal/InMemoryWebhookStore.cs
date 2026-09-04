using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Internal;

/// <summary>
/// Thread-safe, high-performance in-memory implementation of <see cref="IWebhookStore"/> backed by <see cref="ConcurrentDictionary{TKey, TValue}"/>.
/// </summary>
internal sealed class InMemoryWebhookStore : IWebhookStore {
    private readonly ConcurrentDictionary<WebhookJobId, WebhookJobRecord> _jobs = new(WebhookJobId.OrdinalComparer);
    private readonly ConcurrentDictionary<WebhookEndpointId, List<WebhookJobId>> _endpointIndex = new(WebhookEndpointId.OrdinalComparer);
    private readonly Lock _lock = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookStore"/> class using the system clock.
    /// </summary>
    public InMemoryWebhookStore() : this(TimeProvider.System) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryWebhookStore"/> class with a custom <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="timeProvider">The time provider.</param>
    public InMemoryWebhookStore(TimeProvider timeProvider) {
        Preca.ThrowIfNull(timeProvider);
        this._timeProvider = timeProvider;
    }

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
    public Task SaveBatchAsync(IReadOnlyList<WebhookJobRecord> jobs, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(jobs);
        if(jobs.Count == 0) return Task.CompletedTask;

        lock(this._lock) {
            for(int i = 0; i < jobs.Count; i++) {
                WebhookJobRecord job = jobs[i];
                this._jobs[job.Id] = job;

                List<WebhookJobId> list = this._endpointIndex.GetOrAdd(job.EndpointId, static _ => []);
                list.Add(job.Id);
            }
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
            lock(job) {
                job.Status = status;
                if(status == WebhookJobStatus.Retrying) {
                    job.LockedBy = null;
                    job.LockExpiresAt = null;
                }
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken = default) {
        if(this._jobs.TryGetValue(jobId, out WebhookJobRecord? job)) {
            lock(job) {
                job.Status = status;
                job.NextAttemptAt = nextAttemptAt;
                if(status == WebhookJobStatus.Retrying) {
                    job.LockedBy = null;
                    job.LockExpiresAt = null;
                }
            }
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

        DateTimeOffset now = this._timeProvider.GetUtcNow();

        lock(job) {
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
    public Task<IReadOnlyList<WebhookJobRecord>> GetStaleJobsAsync(
       DateTimeOffset? inFlightThreshold,
       DateTimeOffset? queuedThreshold,
       DateTimeOffset? retryingDueThreshold,
       int maxCount,
       CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        List<WebhookJobRecord> stale = [];

        foreach(KeyValuePair<WebhookJobId, WebhookJobRecord> kvp in this._jobs) {
            WebhookJobRecord job = kvp.Value;

            // 1. InFlight job with expired lease (Only checked if inFlightThreshold is provided)
            bool isExpiredInFlight = inFlightThreshold.HasValue
                && job.Status == WebhookJobStatus.InFlight
                && job.LockExpiresAt.HasValue
                && job.LockExpiresAt.Value < inFlightThreshold.Value;

            // 2. Stranded Queued job (Only checked if queuedThreshold is provided)
            bool isStrandedQueued = queuedThreshold.HasValue
                && job.Status == WebhookJobStatus.Queued
                && job.CreatedAt < queuedThreshold.Value
                && (!job.LockExpiresAt.HasValue || (inFlightThreshold.HasValue && job.LockExpiresAt.Value < inFlightThreshold.Value));

            // 3. Orphaned Retrying job whose NextAttemptAt has passed (Only checked if retryingDueThreshold is provided)
            bool isDueRetrying = retryingDueThreshold.HasValue
                && job.Status == WebhookJobStatus.Retrying
                && (!job.NextAttemptAt.HasValue || job.NextAttemptAt.Value <= retryingDueThreshold.Value)
                && (!job.LockExpiresAt.HasValue || (inFlightThreshold.HasValue && job.LockExpiresAt.Value < inFlightThreshold.Value));

            if(isExpiredInFlight || isStrandedQueued || isDueRetrying) {
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