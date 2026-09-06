using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// The quoted column names the claim statement writes, resolved from the EF model.
/// </summary>
/// <remarks>
/// Nothing about the claim SQL may assume a column is named after its property. A context using a naming
/// convention — snake_case is the common one on PostgreSQL — maps <c>Id</c> to <c>id</c> and
/// <c>ProcessedAtTicks</c> to <c>processed_at_ticks</c>, and a statement with the property names in it fails
/// with <c>column "Id" does not exist</c>. The table name was already read from the model; the columns must
/// be too.
/// </remarks>
internal readonly record struct OutboxColumns(
    string Id,
    string LockId,
    string LockExpiresAtTicks,
    string ProcessedAtTicks,
    string DeadLetteredAtTicks,
    string NextAttemptAtTicks,
    string PartitionKey) {

    /// <summary>Reads the mapped column names for <see cref="OutboxMessage"/> out of the model.</summary>
    public static OutboxColumns From(IEntityType entityType, StoreObjectIdentifier storeObject) {
        return new OutboxColumns(
            Quote(entityType, storeObject, nameof(OutboxMessage.Id)),
            Quote(entityType, storeObject, nameof(OutboxMessage.LockId)),
            Quote(entityType, storeObject, nameof(OutboxMessage.LockExpiresAtTicks)),
            Quote(entityType, storeObject, nameof(OutboxMessage.ProcessedAtTicks)),
            Quote(entityType, storeObject, nameof(OutboxMessage.DeadLetteredAtTicks)),
            Quote(entityType, storeObject, nameof(OutboxMessage.NextAttemptAtTicks)),
            Quote(entityType, storeObject, nameof(OutboxMessage.PartitionKey)));
    }

    private static string Quote(IEntityType entityType, StoreObjectIdentifier storeObject, string propertyName) {
        IProperty property = entityType.FindProperty(propertyName)
            ?? throw new InvalidOperationException(
                $"'{nameof(OutboxMessage)}.{propertyName}' is not mapped. The outbox entity must be registered "
                + "through modelBuilder.ApplyDddOutbox().");

        string column = property.GetColumnName(storeObject)
            ?? throw new InvalidOperationException(
                $"'{nameof(OutboxMessage)}.{propertyName}' is not mapped to a column in '{storeObject.Name}'.");

        return $"\"{column}\"";
    }
}
