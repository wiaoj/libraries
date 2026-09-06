using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// Shared plumbing for the relational claim strategies: resolve the mapped table name, run the dialect's
/// statement, and materialize the returned rows.
/// </summary>
internal abstract class RelationalOutboxClaimStrategy : IOutboxClaimStrategy {
    /// <summary>Builds the provider's claim statement. Must claim and return the rows in one statement.</summary>
    internal abstract FormattableString BuildClaim(
        string table,
        int batchSize,
        string workerId,
        long nowTicks,
        long lockExpiresAtTicks,
        string? partitionKey);

    public async Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        string workerId,
        long nowTicks,
        long lockExpiresAtTicks,
        string? partitionKey,
        CancellationToken cancellationToken) {

        string table = ResolveQualifiedTableName(dbContext);

        FormattableString sql = BuildClaim(table, batchSize, workerId, nowTicks, lockExpiresAtTicks, partitionKey);

        List<OutboxMessage> claimed = await dbContext
            .Set<OutboxMessage>()
            .FromSql(sql)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed;
    }

    /// <summary>
    /// Reads the table name from the model rather than assuming it, so a context that renamed the table
    /// through <c>ApplyDddOutbox</c> still claims from the right place.
    /// </summary>
    private static string ResolveQualifiedTableName(DbContext dbContext) {
        IEntityType entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException(
                $"'{typeof(OutboxMessage).Name}' is not part of the model for '{dbContext.GetType().Name}'. " +
                "Call modelBuilder.ApplyDddOutbox() from OnModelCreating.");

        string table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"'{typeof(OutboxMessage).Name}' is not mapped to a table.");

        string? schema = entityType.GetSchema();

        // Quoted so a table or schema name that collides with a keyword still parses. The identifiers come
        // from the model, not from user input at claim time.
        return schema is null
            ? $"\"{table}\""
            : $"\"{schema}\".\"{table}\"";
    }
}
