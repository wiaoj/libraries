using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookBatchStore"/> over the table mapped by <c>ApplyWebhooks</c>.
/// </summary>
/// <remarks>
/// The recovery lease is claimed with one conditional <c>UPDATE</c>, so two instances sweeping the same stale batch
/// cannot both resume it.
/// </remarks>
internal sealed class EfCoreWebhookBatchStore<TDbContext> : IWebhookBatchStore where TDbContext : DbContext {
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;

    public EfCoreWebhookBatchStore(IServiceScopeFactory scopeFactory, TimeProvider timeProvider) {
        Preca.ThrowIfNull(scopeFactory);
        Preca.ThrowIfNull(timeProvider);

        this._scopeFactory = scopeFactory;
        this._timeProvider = timeProvider;
    }

    private WebhookDbContextLease<TDbContext> Open() {
        return WebhookDbContextLease<TDbContext>.Open<WebhookPublishBatchEntity>(this._scopeFactory);
    }

    /// <inheritdoc/>
    public async ValueTask SaveBatchAsync(WebhookPublishBatchRecord batch, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(batch);

        await using WebhookDbContextLease<TDbContext> lease = Open();
        DbSet<WebhookPublishBatchEntity> set = lease.Context.Set<WebhookPublishBatchEntity>();

        string id = batch.Id.Value;
        WebhookPublishBatchEntity? existing = await set
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if(existing is null) {
            set.Add(WebhookEntityMapper.ToEntity(batch));
        }
        else {
            WebhookEntityMapper.CopyState(batch, existing);
        }

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<WebhookPublishBatchRecord?> GetBatchAsync(WebhookBatchId batchId, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = batchId.Value;
        WebhookPublishBatchEntity? entity = await lease.Context.Set<WebhookPublishBatchEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : WebhookEntityMapper.ToBatch(entity);
    }

    /// <inheritdoc/>
    public async ValueTask UpdateBatchProgressAsync(WebhookBatchId batchId, int dispatchedCount, WebhookBatchStatus status, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = batchId.Value;
        long now = WebhookEntityMapper.ToTicks(this._timeProvider.GetUtcNow());

        await lease.Context.Set<WebhookPublishBatchEntity>()
            .Where(b => b.Id == id)
            .ExecuteUpdateAsync(setters => {
                setters.SetProperty(b => b.DispatchedCount, dispatchedCount);
                setters.SetProperty(b => b.Status, status);
                setters.SetProperty(b => b.UpdatedAtTicks, now);
            }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryClaimBatchLeaseAsync(WebhookBatchId batchId, string instanceId, TimeSpan duration, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(instanceId);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = batchId.Value;
        long now = WebhookEntityMapper.ToTicks(this._timeProvider.GetUtcNow());
        long expiresAt = now + duration.Ticks;

        // Same rule as the in-memory store, applied by the database in one statement: free, expired or already ours.
        int affected = await lease.Context.Set<WebhookPublishBatchEntity>()
            .Where(b => b.Id == id
                && (b.LockedBy == null
                    || b.LockExpiresAtTicks == null
                    || b.LockExpiresAtTicks <= now
                    || b.LockedBy == instanceId))
            .ExecuteUpdateAsync(setters => {
                setters.SetProperty(b => b.LockedBy, instanceId);
                setters.SetProperty(b => b.LockExpiresAtTicks, expiresAt);
                setters.SetProperty(b => b.Status, WebhookBatchStatus.InFlight);
            }, cancellationToken)
            .ConfigureAwait(false);

        return affected == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WebhookPublishBatchRecord>> GetStaleInFlightBatchesAsync(DateTimeOffset threshold, int maxCount, CancellationToken cancellationToken = default) {
        Preca.ThrowIfLessThan(maxCount, 1);

        await using WebhookDbContextLease<TDbContext> lease = Open();

        long cutoff = WebhookEntityMapper.ToTicks(threshold);
        List<WebhookPublishBatchEntity> entities = await lease.Context.Set<WebhookPublishBatchEntity>()
            .AsNoTracking()
            .Where(b => b.Status == WebhookBatchStatus.InFlight && b.LockExpiresAtTicks != null && b.LockExpiresAtTicks < cutoff)
            .OrderBy(static b => b.CreatedAtTicks)
            .ThenBy(static b => b.Id)
            .Take(maxCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        WebhookPublishBatchRecord[] batches = new WebhookPublishBatchRecord[entities.Count];
        for(int i = 0; i < batches.Length; i++) {
            batches[i] = WebhookEntityMapper.ToBatch(entities[i]);
        }

        return batches;
    }
}
