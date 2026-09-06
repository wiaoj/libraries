using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// Builds the claim statement's placeholders and arguments, so each dialect only writes the SQL that
/// actually differs.
/// </summary>
/// <remarks>
/// Placeholders are positional (<c>{0}</c>, <c>{1}</c>, …) and every one of them is bound to a parameter by
/// <see cref="FormattableString"/>; only the table name is interpolated directly, and it comes from the EF
/// model rather than from anything a caller supplies.
/// </remarks>
internal readonly struct ClaimSqlArguments {
    /// <summary>Placeholder for the claiming worker's id.</summary>
    public const string WorkerId = "{0}";

    /// <summary>Placeholder for the lock expiry, in UTC ticks.</summary>
    public const string LockExpiresAt = "{1}";

    /// <summary>Placeholder for the current time, in UTC ticks.</summary>
    public const string Now = "{2}";

    /// <summary>Placeholder for the batch size.</summary>
    public const string BatchSize = "{3}";

    /// <summary>Placeholder for the partition key. Only present when one is configured.</summary>
    public const string PartitionKey = "{4}";

    /// <summary>Renders the optional partition predicate, or nothing when no partition is configured.</summary>
    public static string PartitionPredicate(string partitionColumn, string? partitionKey) {
        return partitionKey is null ? string.Empty : $"AND {partitionColumn} = {PartitionKey}";
    }

    /// <summary>
    /// Builds the argument array, appending the partition key only when the predicate referencing it was
    /// rendered — an argument the format string never mentions becomes a parameter nothing binds.
    /// </summary>
    public static object?[] Build(string workerId, long lockExpiresAtTicks, long nowTicks, int batchSize, string? partitionKey) {
        return partitionKey is null
            ? [workerId, lockExpiresAtTicks, nowTicks, batchSize]
            : [workerId, lockExpiresAtTicks, nowTicks, batchSize, partitionKey];
    }
}

/// <summary>
/// PostgreSQL claim. <c>FOR UPDATE SKIP LOCKED</c> lets concurrent processors walk past rows another
/// instance is already claiming instead of queueing behind them, which is what makes horizontal scaling of
/// the processor worth anything.
/// </summary>
internal sealed class PostgresOutboxClaimStrategy : RelationalOutboxClaimStrategy {
    internal override FormattableString BuildClaim(
        string table, OutboxColumns columns, int batchSize, string workerId, long nowTicks, long lockExpiresAtTicks,
        string? partitionKey) {

        string sql =
            $"""
            UPDATE {table} SET {columns.LockId} = {ClaimSqlArguments.WorkerId}, {columns.LockExpiresAtTicks} = {ClaimSqlArguments.LockExpiresAt}
            WHERE {columns.Id} IN (
                SELECT {columns.Id} FROM {table}
                WHERE {columns.ProcessedAtTicks} IS NULL
                  AND {columns.DeadLetteredAtTicks} IS NULL
                  AND {columns.NextAttemptAtTicks} <= {ClaimSqlArguments.Now}
                  AND ({columns.LockId} IS NULL OR {columns.LockExpiresAtTicks} < {ClaimSqlArguments.Now})
                  {ClaimSqlArguments.PartitionPredicate(columns.PartitionKey, partitionKey)}
                ORDER BY {columns.NextAttemptAtTicks}
                LIMIT {ClaimSqlArguments.BatchSize}
                FOR UPDATE SKIP LOCKED
            )
            RETURNING *
            """;

        return FormattableStringFactory.Create(
            sql, ClaimSqlArguments.Build(workerId, lockExpiresAtTicks, nowTicks, batchSize, partitionKey));
    }
}

/// <summary>
/// SQL Server claim. <c>READPAST</c> is the equivalent of skip-locked; <c>UPDLOCK</c> takes the write lock
/// during the read so the chosen rows cannot be taken between the select and the update, and
/// <c>OUTPUT INSERTED.*</c> returns them without a second round trip.
/// </summary>
internal sealed class SqlServerOutboxClaimStrategy : RelationalOutboxClaimStrategy {
    internal override FormattableString BuildClaim(
        string table, OutboxColumns columns, int batchSize, string workerId, long nowTicks, long lockExpiresAtTicks,
        string? partitionKey) {

        string sql =
            $"""
            WITH claimable AS (
                SELECT TOP ({ClaimSqlArguments.BatchSize}) * FROM {table} WITH (ROWLOCK, UPDLOCK, READPAST)
                WHERE {columns.ProcessedAtTicks} IS NULL
                  AND {columns.DeadLetteredAtTicks} IS NULL
                  AND {columns.NextAttemptAtTicks} <= {ClaimSqlArguments.Now}
                  AND ({columns.LockId} IS NULL OR {columns.LockExpiresAtTicks} < {ClaimSqlArguments.Now})
                  {ClaimSqlArguments.PartitionPredicate(columns.PartitionKey, partitionKey)}
                ORDER BY {columns.NextAttemptAtTicks}
            )
            UPDATE claimable SET {columns.LockId} = {ClaimSqlArguments.WorkerId}, {columns.LockExpiresAtTicks} = {ClaimSqlArguments.LockExpiresAt}
            OUTPUT INSERTED.*
            """;

        return FormattableStringFactory.Create(
            sql, ClaimSqlArguments.Build(workerId, lockExpiresAtTicks, nowTicks, batchSize, partitionKey));
    }
}

/// <summary>
/// SQLite claim. SQLite serializes writers, so there is no skip-locked semantics to ask for and none needed;
/// the subquery plus <c>RETURNING</c> still keeps the claim to a single statement and a single round trip.
/// </summary>
internal sealed class SqliteOutboxClaimStrategy : RelationalOutboxClaimStrategy {
    internal override FormattableString BuildClaim(
        string table, OutboxColumns columns, int batchSize, string workerId, long nowTicks, long lockExpiresAtTicks,
        string? partitionKey) {

        string sql =
            $"""
            UPDATE {table} SET {columns.LockId} = {ClaimSqlArguments.WorkerId}, {columns.LockExpiresAtTicks} = {ClaimSqlArguments.LockExpiresAt}
            WHERE {columns.Id} IN (
                SELECT {columns.Id} FROM {table}
                WHERE {columns.ProcessedAtTicks} IS NULL
                  AND {columns.DeadLetteredAtTicks} IS NULL
                  AND {columns.NextAttemptAtTicks} <= {ClaimSqlArguments.Now}
                  AND ({columns.LockId} IS NULL OR {columns.LockExpiresAtTicks} < {ClaimSqlArguments.Now})
                  {ClaimSqlArguments.PartitionPredicate(columns.PartitionKey, partitionKey)}
                ORDER BY {columns.NextAttemptAtTicks}
                LIMIT {ClaimSqlArguments.BatchSize}
            )
            RETURNING *
            """;

        return FormattableStringFactory.Create(
            sql, ClaimSqlArguments.Build(workerId, lockExpiresAtTicks, nowTicks, batchSize, partitionKey));
    }
}

/// <summary>
/// Claim for providers with no SQL at all — the in-memory provider, used in tests.
/// </summary>
/// <remarks>
/// Correctness here means "does not throw, and does not hand the same row to two callers in one process".
/// It is not a production path, so it buys atomicity with a process-wide lock rather than a database one.
/// </remarks>
internal sealed class InMemoryOutboxClaimStrategy : IOutboxClaimStrategy {
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        string workerId,
        long nowTicks,
        long lockExpiresAtTicks,
        string? partitionKey,
        CancellationToken cancellationToken) {

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try {
            IQueryable<OutboxMessage> query = dbContext.Set<OutboxMessage>()
                .Where(m => m.ProcessedAtTicks == null)
                .Where(m => m.DeadLetteredAtTicks == null)
                .Where(m => m.NextAttemptAtTicks <= nowTicks)
                .Where(m => m.LockId == null || m.LockExpiresAtTicks < nowTicks);

            if(partitionKey is not null) {
                query = query.Where(m => m.PartitionKey == partitionKey);
            }

            List<OutboxMessage> claimable = await query
                .OrderBy(m => m.NextAttemptAtTicks)
                .Take(batchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if(claimable.Count == 0) {
                return [];
            }

            foreach(OutboxMessage message in claimable) {
                message.AcquireLock(workerId, lockExpiresAtTicks);
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            foreach(OutboxMessage message in claimable) {
                dbContext.Entry(message).State = EntityState.Detached;
            }

            return claimable;
        }
        finally {
            Gate.Release();
        }
    }
}

/// <summary>
/// Stands in for a provider the outbox has no claim statement for.
/// </summary>
/// <remarks>
/// Failing loudly at the first claim is the point. A silent fallback to a non-atomic claim would appear to
/// work and would hand the same row to several processors under load — the failure would surface as
/// duplicated side effects, far from its cause.
/// </remarks>
internal sealed class UnsupportedOutboxClaimStrategy(string providerName) : IOutboxClaimStrategy {
    public Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        string workerId,
        long nowTicks,
        long lockExpiresAtTicks,
        string? partitionKey,
        CancellationToken cancellationToken) {

        throw new NotSupportedException(
            $"The outbox has no claim strategy for the database provider '{providerName}'. " +
            "Claiming is the one query that cannot be written provider-agnostically, so an unrecognised " +
            "provider fails here rather than falling back to a claim that is not atomic. " +
            "Supported providers: PostgreSQL, SQL Server, SQLite, and the in-memory provider.");
    }
}
