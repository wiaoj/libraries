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
            // Stamping is idempotent by design. EF invokes this interceptor once per SaveChanges
            // *attempt*, and a failed attempt leaves the entity in its pre-save state with the stamp
            // already applied — so does IExecutionStrategy, which re-runs the whole operation over the
            // same tracked graph. Re-stamping would raise a domain exception about an invariant no
            // domain code violated, in place of whatever actually made the first attempt fail.
            // The strict guards stay where they belong: on the domain methods.
            if(entry.Entity is ICreatable createdAudit) {
                if(entry.State == EntityState.Added && createdAudit.CreatedAt == default) {
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
                    // The state flip is unconditional: a soft-deletable entity must never reach the
                    // database as a DELETE, whether or not this attempt is the one that stamped it.
                    entry.State = EntityState.Modified;

                    if(!deletedAudit.IsDeleted) {
                        deletedAudit.Delete(utcNow);
                    }
                }
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}