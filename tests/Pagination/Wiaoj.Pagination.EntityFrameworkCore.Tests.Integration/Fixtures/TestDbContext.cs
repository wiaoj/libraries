using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Primitives.Snowflake;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

public sealed class TestItem {
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class SnowflakeItem {
    public SnowflakeId Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options) {
    public DbSet<TestItem> Items => Set<TestItem>();
    public DbSet<SnowflakeItem> SnowflakeItems => Set<SnowflakeItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        // DateTimeOffset conversion for SQLite
        modelBuilder.Entity<TestItem>()
            .Property(x => x.CreatedAt)
            .HasConversion(
                v => v.ToUnixTimeMilliseconds(),
                v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        // SnowflakeId conversion for SQLite (mapped to long/INTEGER)
        modelBuilder.Entity<SnowflakeItem>()
            .Property(x => x.Id)
            .HasConversion(
                v => v.Value,
                v => new SnowflakeId(v));
    }

    public static (TestDbContext Context, SqliteConnection Connection) CreateInMemoryContext() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        TestDbContext context = new(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}