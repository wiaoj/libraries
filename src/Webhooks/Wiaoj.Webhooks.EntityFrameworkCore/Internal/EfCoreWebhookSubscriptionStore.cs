using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wiaoj.Webhooks.Publishing;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookSubscriptionStore"/> over the table mapped by <c>ApplyWebhooks</c>.
/// </summary>
internal sealed class EfCoreWebhookSubscriptionStore<TDbContext> : IWebhookSubscriptionStore where TDbContext : DbContext {
    private readonly IServiceScopeFactory _scopeFactory;

    public EfCoreWebhookSubscriptionStore(IServiceScopeFactory scopeFactory) {
        Preca.ThrowIfNull(scopeFactory);
        this._scopeFactory = scopeFactory;
    }

    private WebhookDbContextLease<TDbContext> Open() {
        return WebhookDbContextLease<TDbContext>.Open<WebhookSubscriptionEntity>(this._scopeFactory);
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(WebhookNamespace @namespace, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string ns = @namespace.Value;
        List<WebhookSubscriptionEntity> entities = await lease.Context.Set<WebhookSubscriptionEntity>()
            .AsNoTracking()
            .Where(s => s.IsEnabled && s.Namespace == ns)
            .OrderBy(static s => s.CreatedAtTicks)
            .ThenBy(static s => s.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        WebhookSubscription[] subscriptions = new WebhookSubscription[entities.Count];
        for(int i = 0; i < subscriptions.Length; i++) {
            subscriptions[i] = WebhookEntityMapper.ToSubscription(entities[i]);
        }

        return subscriptions;
    }

    /// <inheritdoc/>
    public ValueTask<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(CancellationToken cancellationToken = default) {
        return GetActiveSubscriptionsAsync(WebhookNamespace.Default, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask SaveSubscriptionAsync(WebhookSubscription subscription, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNull(subscription);

        await using WebhookDbContextLease<TDbContext> lease = Open();
        DbSet<WebhookSubscriptionEntity> set = lease.Context.Set<WebhookSubscriptionEntity>();

        string id = subscription.Id.Value;
        WebhookSubscriptionEntity? existing = await set
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        // Saving is an upsert: a subscription saved again replaces the stored one.
        if(existing is null) {
            set.Add(WebhookEntityMapper.ToEntity(subscription));
        }
        else {
            WebhookEntityMapper.CopyState(subscription, existing);
        }

        await lease.Context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = subscriptionId.Value;
        await lease.Context.Set<WebhookSubscriptionEntity>()
            .Where(s => s.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<WebhookSubscription?> GetSubscriptionAsync(WebhookSubscriptionId subscriptionId, CancellationToken cancellationToken = default) {
        await using WebhookDbContextLease<TDbContext> lease = Open();

        string id = subscriptionId.Value;
        WebhookSubscriptionEntity? entity = await lease.Context.Set<WebhookSubscriptionEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : WebhookEntityMapper.ToSubscription(entity);
    }
}
