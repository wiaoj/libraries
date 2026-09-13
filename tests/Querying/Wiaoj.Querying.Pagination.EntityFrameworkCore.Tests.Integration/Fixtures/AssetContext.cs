using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Wiaoj.Querying.Pagination.EntityFrameworkCore.Tests.Integration.Fixtures;

public sealed class Asset {
    public long Id { get; set; }
    public string FileName { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public long FileSize { get; set; }
    public int Priority { get; set; }
    public bool IsDeleted { get; set; }
}

public sealed record AssetSummary(long Id, string FileName, long FileSize);

/// <summary>A public contract: filter and sort by name and size, never see the storage path or deleted rows.</summary>
public class AssetSchema : QuerySchema<Asset, AssetSummary> {
    public AssetSchema() {
        Project(a => new AssetSummary(a.Id, a.FileName, a.FileSize));
        AllowFilter(a => a.FileName);
        AllowFilter(a => a.FileSize);
        AllowSort(a => a.FileSize);
        Property(a => a.Priority).AllowSort().NotInResponse();
        RequireFilter(a => !a.IsDeleted);
        TieBreaker(a => a.Id);
    }
}

public sealed class DefaultSortedAssetSchema : AssetSchema {
    public DefaultSortedAssetSchema() {
        DefaultSort(a => a.FileSize, SortDirection.Descending);
    }
}

public sealed class NoTieBreakerSchema : QuerySchema<Asset, AssetSummary> {
    public NoTieBreakerSchema() {
        Project(a => new AssetSummary(a.Id, a.FileName, a.FileSize));
        AllowSort(a => a.FileSize);
    }
}

public sealed class BrokenContractSchema : QuerySchema<Asset, AssetSummary> {
    public BrokenContractSchema() {
        Project(a => new AssetSummary(a.Id, a.FileName, a.FileSize));
        AllowFilter(a => a.StoragePath);
        TieBreaker(a => a.Id);
    }
}

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

public sealed class AssetContext(DbContextOptions<AssetContext> options) : DbContext(options) {
    public DbSet<Asset> Assets => Set<Asset>();

    public static (AssetContext Context, SqliteConnection Connection, CommandRecorder Recorder) Create() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        CommandRecorder recorder = new();
        AssetContext context = new(new DbContextOptionsBuilder<AssetContext>()
            .UseSqlite(connection)
            .AddInterceptors(recorder)
            .Options);

        context.Database.EnsureCreated();
        return (context, connection, recorder);
    }
}
