using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookStore"/> over the tables mapped by <c>ApplyWebhooks</c>.
/// </summary>
/// <remarks>
/// Lease claims and status transitions are single conditional <c>UPDATE</c> statements, so they are atomic on the
/// database rather than in this process: two instances racing for a lease are decided by the row, and exactly one
/// update matches.
/// </remarks>
internal sealed class EfCoreWebhookStore<TDbContext> : IWebhookStore where TDbContext : DbContext {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public EfCoreWebhookStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) {
        Preca.ThrowIfNull(scopeFactory);
        Preca.ThrowIfNull(timeProvider);

        this._scopeFactory = scopeFactory;
        this._timeProvider = timeProvider;
    }

    private WebhookDbContextLease<TDbContext> Open() {
        return WebhookDbContextLease<TDbContext>.Open<WebhookJobEntity>(this._scopeFactory);
    }

    /// <inheritdoc/>
    public async Task SaveAsync(WebhookJobRecord job, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(job);

        await using WebhookDbContextLease<TDbContext> lease = Open();
        DbSet<WebhookJobEntity> jobs = lease.Context.Set<WebhookJobEntity>();

        string id = job.Id.Value;
        WebhookJobEntity? existing = await jobs
            .Include(static j => j.Attempts)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // Saving a job that already exists replaces its state, as the in-memory store does.
        if(existing is null) {
            jobs.Add(WebhookEntityMapper.ToEntity(job));
        }
        else {
            WebhookEntityMapper.CopyState(job, existing);
        }

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SaveBatchAsync(IReadOnlyList<WebhookJobRecord> jobs, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(jobs);
        if(jobs.Count == 0) return;

        await using WebhookDbContextLease<TDbContext> lease = Open();
        DbSet<WebhookJobEntity> set = lease.Context.Set<WebhookJobEntity>();

        string[] ids = new string[jobs.Count];
        for(int i = 0; i < ids.Length; i++) {
            ids[i] = jobs[i].Id.Value;
        }

        Dictionary<string, WebhookJobEntity> existing = await set
            .Include(static j => j.Attempts)
            .Where(j => ids.Contains(j.Id))
            .ToDictionaryAsync(static j => j.Id, StringComparer.Ordinal, cancellationToken)
            .ConfigureAwait(false);

        foreach(WebhookJobRecord job in jobs) {
            if(existing.TryGetValue(job.Id.Value, out WebhookJobEntity? entity)) {
                WebhookEntityMapper.CopyState(job, entity);
            }
            else {
                entity = WebhookEntityMapper.ToEntity(job);
                existing[job.Id.Value] = entity;
                set.Add(entity);
            }
        }

        // One SaveChanges is one transaction: the batch is persisted entirely or not at all.
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<WebhookJobRecord?> GetJobAsync(WebhookJobId jobId, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = jobId.Value;
        WebhookJobEntity? entity = await lease.Context.Set<WebhookJobEntity>()
            .AsNoTracking()
            .Include(static j => j.Attempts)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : WebhookEntityMapper.ToRecord(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookJobRecord>> GetHistoryByEndpointAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string endpoint = endpointId.Value;
        List<WebhookJobEntity> entities = await lease.Context.Set<WebhookJobEntity>()
            .AsNoTracking()
            .Include(static j => j.Attempts)
            .Where(j => j.EndpointId == endpoint)
            .OrderBy(static j => j.CreatedAtTicks)
            .ThenBy(static j => j.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc/>
    public Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, CancellationToken cancellationToken = default) {
        return UpdateStatusCoreAsync(jobId, status, setNextAttempt: false, nextAttemptAt: null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateStatusAsync(WebhookJobId jobId, WebhookJobStatus status, DateTimeOffset? nextAttemptAt, CancellationToken cancellationToken = default) {
        return UpdateStatusCoreAsync(jobId, status, setNextAttempt: true, nextAttemptAt, cancellationToken);
    }

    private async Task UpdateStatusCoreAsync(
        WebhookJobId jobId,
        WebhookJobStatus status,
        bool setNextAttempt,
        DateTimeOffset? nextAttemptAt,
        CancellationToken cancellationToken) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = jobId.Value;
        bool releasesLease = ReleasesLease(status);
        bool completes = status is WebhookJobStatus.Delivered or WebhookJobStatus.DeadLettered;
        long completedAt = WebhookEntityMapper.ToTicks(this._timeProvider.GetUtcNow());
        long? nextAttemptTicks = WebhookEntityMapper.ToTicks(nextAttemptAt);

        await lease.Context.Set<WebhookJobEntity>()
            .Where(j => j.Id == id)
            .ExecuteUpdateAsync(setters => {
                setters.SetProperty(j => j.Status, status);
                if(releasesLease) {
                    setters.SetProperty(j => j.LockedBy, (string?)null);
                    setters.SetProperty(j => j.LockExpiresAtTicks, (long?)null);
                }

                if(completes) {
                    setters.SetProperty(j => j.CompletedAtTicks, (long?)completedAt);
                }

                if(setNextAttempt) {
                    setters.SetProperty(j => j.NextAttemptAtTicks, nextAttemptTicks);
                }
            }, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool ReleasesLease(WebhookJobStatus status) {
        return status is WebhookJobStatus.Retrying or WebhookJobStatus.Delivered or WebhookJobStatus.DeadLettered;
    }

    /// <inheritdoc/>
    public async Task<bool> TryClaimLeaseAsync(WebhookJobId jobId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(instanceId);
        Preca.ThrowIfNegative(duration);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = jobId.Value;
        long now = WebhookEntityMapper.ToTicks(this._timeProvider.GetUtcNow());
        long expiresAt = now + duration.Ticks;

        // The whole claim rule is the WHERE clause of one UPDATE: the row is updated only while it is unfinished and
        // its lease is free, expired or already ours. The database applies it atomically, so of any number of racing
        // claimants exactly one sees an affected row.
        int affected = await lease.Context.Set<WebhookJobEntity>()
            .Where(j => j.Id == id
                && j.Status != WebhookJobStatus.Delivered
                && j.Status != WebhookJobStatus.DeadLettered
                && (j.LockedBy == null
                    || j.LockExpiresAtTicks == null
                    || j.LockExpiresAtTicks <= now
                    || j.LockedBy == instanceId))
            .ExecuteUpdateAsync(setters => {
                setters.SetProperty(j => j.LockedBy, instanceId);
                setters.SetProperty(j => j.LockExpiresAtTicks, expiresAt);
                setters.SetProperty(j => j.Status, WebhookJobStatus.InFlight);
            }, cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc/>
    public async Task RecordAttemptAsync(WebhookJobId jobId, WebhookDeliveryAttempt attempt, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(attempt);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = jobId.Value;
        bool exists = await lease.Context.Set<WebhookJobEntity>()
            .AnyAsync(j => j.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // An attempt for a job the store does not track is dropped, as the in-memory store does.
        if(!exists) return;

        lease.Context.Set<WebhookDeliveryAttemptEntity>().Add(WebhookEntityMapper.ToEntity(jobId, attempt));
        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookJobRecord>> GetStaleJobsAsync(
        DateTimeOffset? inFlightThreshold,
        DateTimeOffset? queuedThreshold,
        DateTimeOffset? retryingDueThreshold,
        int maxCount,
        CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        if(inFlightThreshold is null && queuedThreshold is null && retryingDueThreshold is null) {
            return [];
        }

        await using WebhookDbContextLease<TDbContext> lease = Open();

        long? inFlight = WebhookEntityMapper.ToTicks(inFlightThreshold);
        long? queued = WebhookEntityMapper.ToTicks(queuedThreshold);
        long? retryingDue = WebhookEntityMapper.ToTicks(retryingDueThreshold);

        // The same three rules as the in-memory store: an in-flight job whose lease expired, a queued job nobody picked
        // up, and a retrying job whose retry is due. A queued or retrying job that still holds an unexpired lease is
        // being handled and is not stale.
        List<WebhookJobEntity> entities = await lease.Context.Set<WebhookJobEntity>()
            .AsNoTracking()
            .Include(static j => j.Attempts)
            .Where(j =>
                (inFlight != null
                    && j.Status == WebhookJobStatus.InFlight
                    && j.LockExpiresAtTicks != null
                    && j.LockExpiresAtTicks < inFlight)
                || (queued != null
                    && j.Status == WebhookJobStatus.Queued
                    && j.CreatedAtTicks < queued
                    && (j.LockExpiresAtTicks == null || (inFlight != null && j.LockExpiresAtTicks < inFlight)))
                || (retryingDue != null
                    && j.Status == WebhookJobStatus.Retrying
                    && (j.NextAttemptAtTicks == null || j.NextAttemptAtTicks <= retryingDue)
                    && (j.LockExpiresAtTicks == null || (inFlight != null && j.LockExpiresAtTicks < inFlight))))
            .OrderBy(static j => j.CreatedAtTicks)
            .ThenBy(static j => j.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        List<WebhookJobEntity> entities = await lease.Context.Set<WebhookJobEntity>()
            .AsNoTracking()
            .Include(static j => j.Attempts)
            .Where(static j => j.Status == WebhookJobStatus.DeadLettered)
            .OrderBy(static j => j.CreatedAtTicks)
            .ThenBy(static j => j.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookJobRecord>> GetDeadLetteredJobsAsync(WebhookEndpointId endpointId, int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        string endpoint = endpointId.Value;
        List<WebhookJobEntity> entities = await lease.Context.Set<WebhookJobEntity>()
            .AsNoTracking()
            .Include(static j => j.Attempts)
            .Where(j => j.EndpointId == endpoint && j.Status == WebhookJobStatus.DeadLettered)
            .OrderBy(static j => j.CreatedAtTicks)
            .ThenBy(static j => j.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return ToRecords(entities);
    }

    /// <inheritdoc/>
    public async Task<int> PruneJobsAsync(DateTimeOffset deliveredBefore, DateTimeOffset deadLetteredBefore, int batchSize, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(batchSize, 1);

        await using WebhookDbContextLease<TDbContext> lease = Open();
        DbContext context = lease.Context;

        long delivered = WebhookEntityMapper.ToTicks(deliveredBefore);
        long deadLettered = WebhookEntityMapper.ToTicks(deadLetteredBefore);

        // One batch is one transaction: the ids are chosen, their attempts deleted and then the jobs, or none of it.
        // The attempts are deleted explicitly rather than left to the foreign key, so a provider that does not
        // enforce cascades (SQLite without foreign keys on) leaves no orphans either.
        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        List<string> ids = await context.Set<WebhookJobEntity>()
            .Where(j => j.CompletedAtTicks != null
                && ((j.Status == WebhookJobStatus.Delivered && j.CompletedAtTicks < delivered)
                    || (j.Status == WebhookJobStatus.DeadLettered && j.CompletedAtTicks < deadLettered)))
            .OrderBy(static j => j.CompletedAtTicks)
            .ThenBy(static j => j.Id)
            .Select(static j => j.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if(ids.Count == 0) {
            return 0;
        }

        await context.Set<WebhookDeliveryAttemptEntity>()
            .Where(a => ids.Contains(a.JobId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        // The status is checked again: a job cannot leave a finished state, but the rule stays with the statement.
        int deleted = await context.Set<WebhookJobEntity>()
            .Where(j => ids.Contains(j.Id)
                && (j.Status == WebhookJobStatus.Delivered || j.Status == WebhookJobStatus.DeadLettered))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return deleted;
    }

    private static WebhookJobRecord[] ToRecords(List<WebhookJobEntity> entities) {
        WebhookJobRecord[] records = new WebhookJobRecord[entities.Count];
        for(int i = 0; i < records.Length; i++) {
            records[i] = WebhookEntityMapper.ToRecord(entities[i]);
        }

        return records;
    }
}
