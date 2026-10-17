using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Webhooks.EntityFrameworkCore.Tests.Integration.Infrastructure;

/// <summary>
/// An application context that hosts the webhook tables next to its own, the way a consumer would.
/// </summary>
public sealed class TestWebhookDbContext(DbContextOptions<TestWebhookDbContext> options) : DbContext(options) {
    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.ApplyWebhooks();
    }
}

/// <summary>
/// A context that forgot to call <c>ApplyWebhooks</c>.
/// </summary>
public sealed class UnmappedDbContext(DbContextOptions<UnmappedDbContext> options) : DbContext(options);
