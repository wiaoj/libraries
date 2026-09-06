using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

/// <summary>
/// A strongly-typed identifier of the shape a DDD codebase actually produces: a wrapper over a
/// primitive, ordered through <see cref="IComparable{T}"/>, with equality but <b>no</b> relational
/// operators. This is the case issue #57 reports — the underlying primitive is unreachable through a
/// member access EF Core could translate, so the caller cannot supply a primitive key selector.
/// </summary>
public readonly struct DeliveryRef(long value) : IComparable<DeliveryRef>, IEquatable<DeliveryRef> {
    public long Value { get; } = value;

    public int CompareTo(DeliveryRef other) => this.Value.CompareTo(other.Value);
    public bool Equals(DeliveryRef other) => this.Value == other.Value;
    public override bool Equals(object? obj) => obj is DeliveryRef other && Equals(other);
    public override int GetHashCode() => this.Value.GetHashCode();
    public override string ToString() => this.Value.ToString();
}

/// <summary>
/// The same identifier shape, but additionally declaring relational operators. Included to pin down
/// exactly which half of the contract the seek predicate depends on.
/// </summary>
public readonly struct ComparableRef(long value) : IComparable<ComparableRef>, IEquatable<ComparableRef> {
    public long Value { get; } = value;

    public int CompareTo(ComparableRef other) => this.Value.CompareTo(other.Value);
    public bool Equals(ComparableRef other) => this.Value == other.Value;
    public override bool Equals(object? obj) => obj is ComparableRef other && Equals(other);
    public override int GetHashCode() => this.Value.GetHashCode();
    public override string ToString() => this.Value.ToString();

    public static bool operator <(ComparableRef left, ComparableRef right) => left.Value < right.Value;
    public static bool operator >(ComparableRef left, ComparableRef right) => left.Value > right.Value;
    public static bool operator <=(ComparableRef left, ComparableRef right) => left.Value <= right.Value;
    public static bool operator >=(ComparableRef left, ComparableRef right) => left.Value >= right.Value;
    public static bool operator ==(ComparableRef left, ComparableRef right) => left.Equals(right);
    public static bool operator !=(ComparableRef left, ComparableRef right) => !left.Equals(right);
}

public sealed class DeliveryLog {
    public DeliveryRef RequestId { get; set; }
    public string Payload { get; set; } = string.Empty;
}

public sealed class ComparableLog {
    public ComparableRef RequestId { get; set; }
    public string Payload { get; set; } = string.Empty;
}

/// <summary>
/// Context whose keys are value objects reaching the database through a <c>ValueConverter</c>,
/// exactly as a strongly-typed snowflake id would.
/// </summary>
public sealed class ValueConvertedKeyContext(DbContextOptions<ValueConvertedKeyContext> options) : DbContext(options) {
    public DbSet<DeliveryLog> DeliveryLogs => Set<DeliveryLog>();
    public DbSet<ComparableLog> ComparableLogs => Set<ComparableLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DeliveryLog>(entity => {
            entity.HasKey(x => x.RequestId);
            entity.Property(x => x.RequestId)
                .HasConversion(v => v.Value, v => new DeliveryRef(v));
        });

        modelBuilder.Entity<ComparableLog>(entity => {
            entity.HasKey(x => x.RequestId);
            entity.Property(x => x.RequestId)
                .HasConversion(v => v.Value, v => new ComparableRef(v));
        });
    }

    public static (ValueConvertedKeyContext Context, SqliteConnection Connection) CreateInMemoryContext() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        DbContextOptions<ValueConvertedKeyContext> options = new DbContextOptionsBuilder<ValueConvertedKeyContext>()
            .UseSqlite(connection)
            .Options;

        ValueConvertedKeyContext context = new(options);
        context.Database.EnsureCreated();

        return (context, connection);
    }
}

/// <summary>
/// Satisfies the <see cref="IComparable{T}"/> constraint through an <b>explicit</b> implementation, so no
/// public <c>CompareTo</c> and no relational operators exist. Nothing here can be translated to SQL, and
/// the library is expected to say so plainly rather than emit a broken query.
/// </summary>
public readonly struct OpaqueRef(long value) : IComparable<OpaqueRef> {
    public long Value { get; } = value;

    int IComparable<OpaqueRef>.CompareTo(OpaqueRef other) => this.Value.CompareTo(other.Value);
}
