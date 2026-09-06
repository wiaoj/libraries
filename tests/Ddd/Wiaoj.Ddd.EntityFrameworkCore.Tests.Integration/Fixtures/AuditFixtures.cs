using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Ddd;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.ValueObjects;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;

public readonly record struct OrderId(long Value) : IId;

public sealed class Order : Aggregate<OrderId> {
    private Order() { }

    public Order(OrderId id, string reference) : base(id) {
        this.Reference = reference;
    }

    public string Reference { get; private set; } = string.Empty;

    public void Rename(string reference) => this.Reference = reference;
}

public sealed class AuditTestContext(DbContextOptions<AuditTestContext> options) : DbContext(options) {
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(v => v.Value, v => new OrderId(v));
            entity.Property(x => x.Reference).IsRequired();
            entity.Ignore(x => x.DomainEvents);
            entity.Ignore(x => x.Version);

            // A unique index gives the tests a deterministic way to make the first SaveChanges fail
            // the way a real one does: after the failure the entity is still Added, and still stamped.
            entity.HasIndex(x => x.Reference).IsUnique();
        });
    }

    public static (AuditTestContext Context, SqliteConnection Connection) Create(TimeProvider timeProvider) {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<AuditTestContext> options = new DbContextOptionsBuilder<AuditTestContext>()
            .UseSqlite(connection)
            .AddInterceptors(new AuditInterceptor(timeProvider))
            .Options;

        AuditTestContext context = new(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}
