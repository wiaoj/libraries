using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal;
public sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor {
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
        DbContext? context = eventData.Context;

        if(context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        DateTimeOffset utcNow = timeProvider.GetUtcNow();

        foreach(EntityEntry entry in context.ChangeTracker.Entries()) {
            if(entry.Entity is ICreatable createdAudit) {
                if(entry.State == EntityState.Added) {
                    createdAudit.SetCreatedAt(utcNow);
                }
            }

            if(entry.Entity is IUpdatable updatedAudit) {
                if(entry.State == EntityState.Modified) {
                    updatedAudit.SetUpdatedAt(utcNow);
                }
            }

            if(entry.Entity is IDeletable deletedAudit) {
                if(entry.State == EntityState.Deleted) {
                    entry.State = EntityState.Modified;
                    deletedAudit.Delete(utcNow);
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}