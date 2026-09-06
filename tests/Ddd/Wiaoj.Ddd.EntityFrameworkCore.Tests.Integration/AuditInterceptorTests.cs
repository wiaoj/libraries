using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Ddd.Exceptions;
using Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

// -----------------------------------------------------------------------------------------------
// Issue #62: EF calls SavingChangesAsync once per SaveChanges *attempt*. A failed attempt leaves the
// entity Added and already stamped, so the next attempt re-enters the interceptor and the domain
// guard fires — reporting an invariant violation that never happened, in place of the error that
// actually caused the first attempt to fail.
// -----------------------------------------------------------------------------------------------
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "AuditInterceptor")]
public sealed class AuditInterceptorTests {

    private static readonly DateTimeOffset Origin = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public sealed class TheCreatedAtStamp {
        [Fact]
        public async Task Should_Stamp_CreatedAt_On_Insert() {
            FakeTimeProvider time = new(Origin);
            (AuditTestContext context, SqliteConnection connection) = AuditTestContext.Create(time);

            try {
                Order order = new(new OrderId(1), "ref-1");
                context.Orders.Add(order);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                Assert.Equal(Origin, order.CreatedAt);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Survive_A_Retried_Save_After_The_First_Attempt_Failed() {
            FakeTimeProvider time = new(Origin);
            (AuditTestContext context, SqliteConnection connection) = AuditTestContext.Create(time);

            try {
                // Occupy the unique reference so the first attempt fails the way a real one does.
                context.Orders.Add(new Order(new OrderId(1), "duplicate"));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                Order clashing = new(new OrderId(2), "duplicate");
                context.Orders.Add(clashing);

                await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken));

                // The entity is still Added and already stamped. Retrying the save — after fixing the
                // cause, as a caller would — must not report a domain invariant violation.
                clashing.Rename("resolved");

                DbUpdateException? secondFailure = await Record.ExceptionAsync(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken)) as DbUpdateException;

                Assert.Null(secondFailure);
                Assert.Equal(2, await context.Orders.CountAsync(TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Not_Restamp_CreatedAt_On_A_Retried_Save() {
            FakeTimeProvider time = new(Origin);
            (AuditTestContext context, SqliteConnection connection) = AuditTestContext.Create(time);

            try {
                context.Orders.Add(new Order(new OrderId(1), "duplicate"));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                Order clashing = new(new OrderId(2), "duplicate");
                context.Orders.Add(clashing);

                await Assert.ThrowsAnyAsync<DbUpdateException>(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken));

                // Time moves on between the two attempts; the original stamp must win.
                time.Advance(TimeSpan.FromMinutes(5));
                clashing.Rename("resolved");
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                Assert.Equal(Origin, clashing.CreatedAt);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }

    public sealed class TheSoftDeleteStamp {
        [Fact]
        public async Task Should_Soft_Delete_Instead_Of_Removing_The_Row() {
            FakeTimeProvider time = new(Origin);
            (AuditTestContext context, SqliteConnection connection) = AuditTestContext.Create(time);

            try {
                Order order = new(new OrderId(1), "ref-1");
                context.Orders.Add(order);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                context.Orders.Remove(order);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                Assert.True(order.IsDeleted);
                Assert.Equal(1, await context.Orders.CountAsync(TestContext.Current.CancellationToken));
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }

        [Fact]
        public async Task Should_Survive_A_Retried_Save_Of_An_Already_Stamped_Deletion() {
            FakeTimeProvider time = new(Origin);
            (AuditTestContext context, SqliteConnection connection) = AuditTestContext.Create(time);

            try {
                Order order = new(new OrderId(1), "ref-1");
                context.Orders.Add(order);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);

                // Stamp the deletion by hand, as a failed first attempt would have left it, then mark
                // the entry Deleted so the interceptor sees the same state it would on a retry.
                order.Delete(time.GetUtcNow());
                context.Entry(order).State = EntityState.Deleted;

                EntityAlreadyDeletedException? failure = await Record.ExceptionAsync(() =>
                    context.SaveChangesAsync(TestContext.Current.CancellationToken)) as EntityAlreadyDeletedException;

                Assert.Null(failure);
            }
            finally {
                await context.DisposeAsync();
                await connection.DisposeAsync();
            }
        }
    }
}
