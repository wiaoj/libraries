using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// A <typeparamref name="TDbContext"/> resolved from its own service scope, for one store operation.
/// </summary>
/// <remarks>
/// The webhook stores are singletons, consumed by the singleton dispatcher and the background workers, while a
/// <see cref="DbContext"/> is scoped and not thread-safe. Each operation therefore takes a fresh context and disposes
/// it with the scope.
/// </remarks>
internal readonly struct WebhookDbContextLease<TDbContext> : IAsyncDisposable where TDbContext : DbContext {
    private readonly AsyncServiceScope _scope;

    public TDbContext Context { get; }

    private WebhookDbContextLease(AsyncServiceScope scope, TDbContext context) {
        this._scope = scope;
        this.Context = context;
    }

    /// <summary>
    /// Opens a scope and resolves the context, checking that <c>ApplyWebhooks</c> mapped <typeparamref name="TEntity"/>.
    /// </summary>
    public static WebhookDbContextLease<TDbContext> Open<TEntity>(IServiceScopeFactory scopeFactory) where TEntity : class {
        AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        try {
            TDbContext context = scope.ServiceProvider.GetRequiredService<TDbContext>();
            if(context.Model.FindEntityType(typeof(TEntity)) is null) {
                throw new InvalidOperationException(
                    $"'{typeof(TDbContext).Name}' does not map '{typeof(TEntity).Name}'. Call modelBuilder.ApplyWebhooks() in its OnModelCreating.");
            }

            return new WebhookDbContextLease<TDbContext>(scope, context);
        }
        catch {
            scope.Dispose();
            throw;
        }
    }

    public ValueTask DisposeAsync() {
        return this._scope.DisposeAsync();
    }
}
