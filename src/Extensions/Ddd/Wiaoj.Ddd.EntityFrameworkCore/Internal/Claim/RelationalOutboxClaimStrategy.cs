using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// Shared plumbing for the relational claim strategies: resolve the mapped table and column names, run the
/// dialect's statement, and materialize the returned rows.
/// </summary>
/// <remarks>
/// Both the table and every column name come from the EF model rather than from the property names, so a
/// context using a naming convention — snake_case on PostgreSQL, say — claims from the columns it actually
/// mapped.
/// </remarks>
internal abstract class RelationalOutboxClaimStrategy : IOutboxClaimStrategy {
    /// <summary>Builds the provider's claim statement. Must claim and return the rows in one statement.</summary>
    internal abstract FormattableString BuildClaim(
        string table,
        OutboxColumns columns,
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

        (string table, OutboxColumns columns) = ResolveMapping(dbContext);

        FormattableString sql = BuildClaim(table, columns, batchSize, workerId, nowTicks, lockExpiresAtTicks, partitionKey);

        List<OutboxMessage> claimed = await dbContext
            .Set<OutboxMessage>()
            .FromSql(sql)
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed;
    }

    /// <summary>
    /// Reads the qualified table name and the mapped column names out of the model.
    /// </summary>
    private static (string Table, OutboxColumns Columns) ResolveMapping(DbContext dbContext) {
        IEntityType entityType = dbContext.Model.FindEntityType(typeof(OutboxMessage))
            ?? throw new InvalidOperationException(
                $"'{typeof(OutboxMessage).Name}' is not part of the model for '{dbContext.GetType().Name}'. " +
                "Call modelBuilder.ApplyDddOutbox() from OnModelCreating.");

        string table = entityType.GetTableName()
            ?? throw new InvalidOperationException($"'{typeof(OutboxMessage).Name}' is not mapped to a table.");

        string? schema = entityType.GetSchema();

        StoreObjectIdentifier storeObject = StoreObjectIdentifier.Table(table, schema);
        OutboxColumns columns = OutboxColumns.From(entityType, storeObject);

        // Quoted so a table or schema name that collides with a keyword still parses, and so a lower-case
        // name survives a provider that would otherwise fold identifiers. Both come from the model, never
        // from anything a caller supplies at claim time.
        string qualified = schema is null
            ? $"\"{table}\""
            : $"\"{schema}\".\"{table}\"";

        return (qualified, columns);
    }
}
