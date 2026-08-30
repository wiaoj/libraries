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

public sealed class GuidItem {
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Dedicated fixture entity exposing every small/unsigned integer primitive type supported by the
/// keyset cursor pagination overloads (<see cref="byte"/>, <see cref="sbyte"/>, <see cref="short"/>,
/// <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>, <see cref="ulong"/>), none of which
/// exist on <see cref="TestItem"/>. The <see cref="Id"/> property intentionally exists (as on any
/// realistic entity) so tests can exercise the library's automatic <c>Id</c>-based tie-breaker
/// detection for the overloads that support it (currently <see cref="int"/>).
/// </summary>
public sealed class SmallKeyItem {
    public long Id { get; set; }
    public byte ByteKey { get; set; }
    public sbyte SByteKey { get; set; }
    public short ShortKey { get; set; }
    public ushort UShortKey { get; set; }
    public int IntKey { get; set; }
    public uint UIntKey { get; set; }
    public ulong ULongKey { get; set; }
    public string Label { get; set; } = string.Empty;
}


public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options) {
    public DbSet<TestItem> Items => Set<TestItem>();
    public DbSet<SnowflakeItem> SnowflakeItems => Set<SnowflakeItem>();
    public DbSet<GuidItem> GuidItems => Set<GuidItem>();
    public DbSet<SmallKeyItem> SmallKeyItems => Set<SmallKeyItem>();

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

        // SQLite has no native unsigned or sub-32-bit integer storage class, so every non-int,
        // non-long numeric key on SmallKeyItem is explicitly converted to/from long to guarantee
        // reliable round-tripping (mirrors the SnowflakeId conversion pattern above).
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.ByteKey)
            .HasConversion(v => (long)v, v => (byte)v);

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.SByteKey)
            .HasConversion(v => (long)v, v => (sbyte)v);

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.ShortKey)
            .HasConversion(v => (long)v, v => (short)v);

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.UShortKey)
            .HasConversion(v => (long)v, v => (ushort)v);

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.UIntKey)
            .HasConversion(v => (long)v, v => (uint)v);

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.ULongKey)
            .HasConversion(v => (long)v, v => (ulong)v);
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