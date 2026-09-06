using Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// PostgreSQL and SQL Server cannot be exercised end-to-end here, so their claim statements are pinned by
/// shape instead. The properties asserted are the ones the design depends on: skip-locked semantics so
/// concurrent processors do not queue behind each other, a single statement that both claims and returns,
/// and every value bound as a parameter rather than interpolated.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxClaimSqlTests {

    private static FormattableString Build(string provider, string? partitionKey = null) {
        RelationalOutboxClaimStrategy strategy = provider switch {
            "postgres" => new PostgresOutboxClaimStrategy(),
            "sqlserver" => new SqlServerOutboxClaimStrategy(),
            _ => new SqliteOutboxClaimStrategy()
        };

        OutboxColumns columns = new("\"Id\"", "\"LockId\"", "\"LockExpiresAtTicks\"", "\"ProcessedAtTicks\"",
            "\"DeadLetteredAtTicks\"", "\"NextAttemptAtTicks\"", "\"PartitionKey\"");

        return strategy.BuildClaim("\"OutboxMessages\"", columns, batchSize: 20, workerId: "worker-1",
            nowTicks: 100, lockExpiresAtTicks: 200, partitionKey: partitionKey);
    }

    [Fact]
    public void Postgres_SkipsRowsLockedByAnotherProcessor() {
        FormattableString sql = Build("postgres");

        Assert.Contains("FOR UPDATE SKIP LOCKED", sql.Format, StringComparison.Ordinal);
        Assert.Contains("RETURNING *", sql.Format, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_SkipsRowsLockedByAnotherProcessor() {
        FormattableString sql = Build("sqlserver");

        Assert.Contains("READPAST", sql.Format, StringComparison.Ordinal);
        Assert.Contains("UPDLOCK", sql.Format, StringComparison.Ordinal);
        Assert.Contains("OUTPUT INSERTED.*", sql.Format, StringComparison.Ordinal);
    }

    [Fact]
    public void Sqlite_ClaimsAndReturnsInOneStatement() {
        FormattableString sql = Build("sqlite");

        Assert.Contains("RETURNING *", sql.Format, StringComparison.Ordinal);
        Assert.Contains("LIMIT", sql.Format, StringComparison.Ordinal);
    }

    public static TheoryData<string> Strategies => ["postgres", "sqlserver", "sqlite"];

    [Theory]
    [MemberData(nameof(Strategies))]
    public void EveryStrategy_OnlyClaimsRowsThatAreNeitherProcessedNorDeadLettered(string strategy) {
        FormattableString sql = Build(strategy);

        Assert.Contains(@"""ProcessedAtTicks"" IS NULL", sql.Format, StringComparison.Ordinal);
        Assert.Contains(@"""DeadLetteredAtTicks"" IS NULL", sql.Format, StringComparison.Ordinal);
        Assert.Contains(@"ORDER BY ""NextAttemptAtTicks""", sql.Format, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void EveryStrategy_BindsItsValuesAsParameters(string strategy) {
        FormattableString sql = Build(strategy);

        // The worker id and the timestamps must never be interpolated into the text.
        Assert.DoesNotContain("worker-1", sql.Format, StringComparison.Ordinal);
        Assert.Contains("worker-1", sql.GetArguments().Select(a => a?.ToString()));
        Assert.Equal(4, sql.ArgumentCount);
    }

    [Theory]
    [MemberData(nameof(Strategies))]
    public void EveryStrategy_AddsThePartitionArgumentOnlyWhenItFiltersOnOne(string strategy) {
        // An argument the format string never references becomes a parameter nothing binds.
        Assert.Equal(4, Build(strategy).ArgumentCount);

        FormattableString partitioned = Build(strategy, partitionKey: "eu-west");
        Assert.Equal(5, partitioned.ArgumentCount);
        Assert.Contains(@"""PartitionKey""", partitioned.Format, StringComparison.Ordinal);
    }
}
