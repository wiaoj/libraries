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
/// Dedicated fixture entity exposing every primitive key type supported by the keyset cursor
/// pagination overloads (<see cref="byte"/>, <see cref="sbyte"/>, <see cref="short"/>,
/// <see cref="ushort"/>, <see cref="int"/>, <see cref="uint"/>, <see cref="long"/> (as
/// <see cref="CategoryId"/>, deliberately non-unique - <see cref="Id"/> already covers the unique
/// long case), <see cref="ulong"/>, <see cref="Int128"/>, <see cref="UInt128"/>, <see cref="double"/>,
/// <see cref="float"/>, <see cref="Half"/>, <see cref="DateOnly"/>, <see cref="TimeOnly"/>,
/// <see cref="TimeSpan"/>, <see cref="char"/>), none of which exist on <see cref="TestItem"/>.
/// The <see cref="Id"/> property intentionally exists (as on any realistic entity) so tests can
/// exercise the library's automatic <c>Id</c>-based tie-breaker detection across every type.
/// </summary>
public sealed class SmallKeyItem {
    public long Id { get; set; }
    public byte ByteKey { get; set; }
    public sbyte SByteKey { get; set; }
    public short ShortKey { get; set; }
    public ushort UShortKey { get; set; }
    public int IntKey { get; set; }
    public uint UIntKey { get; set; }
    public long CategoryId { get; set; }
    public ulong ULongKey { get; set; }
    public Int128 Int128Key { get; set; }
    public UInt128 UInt128Key { get; set; }
    public double DoubleKey { get; set; }
    public float FloatKey { get; set; }
    public Half HalfKey { get; set; }
    public DateOnly DateOnlyKey { get; set; }
    public TimeOnly TimeOnlyKey { get; set; }
    public TimeSpan TimeSpanKey { get; set; }
    public char CharKey { get; set; }
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

        // Int128/UInt128 exceed SQLite's native 64-bit INTEGER storage class, so both are
        // round-tripped through their invariant-culture string representation (TEXT column).
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.Int128Key)
            .HasConversion(
                v => v.ToString(),
                v => Int128.Parse(v));

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.UInt128Key)
            .HasConversion(
                v => v.ToString(),
                v => UInt128.Parse(v));

        // Half has no native SQLite storage class; round-tripped through double (REAL column).
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.HalfKey)
            .HasConversion(
                v => (double)v,
                v => (Half)v);

        // DateOnly is stored as its DayNumber (INTEGER) - matches the binary cursor encoding
        // used by the corresponding ToCursorResultAsync overload.
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.DateOnlyKey)
            .HasConversion(
                v => v.DayNumber,
                v => DateOnly.FromDayNumber(v));

        // TimeOnly/TimeSpan are stored as their Ticks (INTEGER) - matches the binary cursor
        // encoding used by the corresponding ToCursorResultAsync overloads.
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.TimeOnlyKey)
            .HasConversion(
                v => v.Ticks,
                v => new TimeOnly(v));

        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.TimeSpanKey)
            .HasConversion(
                v => v.Ticks,
                v => new TimeSpan(v));

        // char has no native SQLite storage class; round-tripped through its ushort code point
        // (INTEGER column) - matches the binary cursor encoding used by the char overload.
        modelBuilder.Entity<SmallKeyItem>()
            .Property(x => x.CharKey)
            .HasConversion(
                v => (int)v,
                v => (char)v);
    }

    public static (TestDbContext Context, SqliteConnection Connection) CreateInMemoryContext() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<TestDbContext> options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .Options;

        TestDbContext context = new(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}