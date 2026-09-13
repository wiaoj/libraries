using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Wiaoj.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

/// <summary>
/// A strongly-typed identifier as this codebase writes one: a <c>readonly record struct</c>, so it has <c>==</c>
/// and <c>!=</c> from the compiler, is comparable, has an encoding for the wire, and has no relational operators.
/// </summary>
public readonly record struct AssetId(long Value) : IComparable<AssetId> {
    /// <summary>Not translatable to SQL; usable only in a final projection, where EF Core evaluates on the client.</summary>
    public string Encode() => $"as_{this.Value}";

    public int CompareTo(AssetId other) => this.Value.CompareTo(other.Value);
}

/// <summary>The same identifier written as a plain struct, without the equality operators a record struct gets.</summary>
public readonly struct OperatorlessAssetId(long value) : IComparable<OperatorlessAssetId>, IEquatable<OperatorlessAssetId> {
    public long Value { get; } = value;

    public string Encode() => $"as_{this.Value}";

    public int CompareTo(OperatorlessAssetId other) => this.Value.CompareTo(other.Value);
    public bool Equals(OperatorlessAssetId other) => this.Value == other.Value;
    public override bool Equals(object? obj) => obj is OperatorlessAssetId other && Equals(other);
    public override int GetHashCode() => this.Value.GetHashCode();
}

public sealed class OperatorlessAsset {
    public OperatorlessAssetId Id { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public sealed class Asset {
    public AssetId Id { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>Never part of a response; a projection that works reads no such column.</summary>
    public string StoragePath { get; set; } = string.Empty;

    public long FileSize { get; set; }

    /// <summary>Deliberately low-cardinality, so paging on it depends on the tie-breaker.</summary>
    public int Priority { get; set; }
}

public sealed record AssetSummaryResponse(string Id, string FileName, long FileSize);

/// <summary>The shape response DTOs usually take, with the key carried as a member.</summary>
public sealed record PositionalAssetRow(AssetId Key, string FileName);

/// <summary>Records every command the context sends, so a test can read the SQL it produced.</summary>
public sealed class CommandRecorder : DbCommandInterceptor {
    public List<string> Commands { get; } = [];

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {
        this.Commands.Add(command.CommandText);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default) {
        this.Commands.Add(command.CommandText);
        return ValueTask.FromResult(result);
    }
}

public sealed class ProjectionContext(DbContextOptions<ProjectionContext> options) : DbContext(options) {
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<OperatorlessAsset> OperatorlessAssets => Set<OperatorlessAsset>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<OperatorlessAsset>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(v => v.Value, v => new OperatorlessAssetId(v));
        });

        modelBuilder.Entity<Asset>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasConversion(v => v.Value, v => new AssetId(v));
        });
    }

    public static (ProjectionContext Context, SqliteConnection Connection, CommandRecorder Recorder) Create() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        CommandRecorder recorder = new();
        DbContextOptions<ProjectionContext> options = new DbContextOptionsBuilder<ProjectionContext>()
            .UseSqlite(connection)
            .AddInterceptors(recorder)
            .Options;

        ProjectionContext context = new(options);
        context.Database.EnsureCreated();

        return (context, connection, recorder);
    }
}
