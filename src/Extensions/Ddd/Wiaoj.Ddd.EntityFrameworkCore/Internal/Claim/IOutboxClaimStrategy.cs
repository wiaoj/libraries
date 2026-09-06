using Microsoft.EntityFrameworkCore;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;

namespace Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;

/// <summary>
/// Atomically takes ownership of a batch of claimable rows and returns them.
/// </summary>
/// <remarks>
/// <para>
/// This is the outbox's hot path and the one query LINQ cannot express. <c>ExecuteUpdate</c> emits a plain
/// <c>UPDATE</c>: there is no way to ask for <c>FOR UPDATE SKIP LOCKED</c> (PostgreSQL) or
/// <c>READPAST</c> (SQL Server), so concurrent processors either block on each other's row locks or claim
/// overlapping candidate sets and lose the update. It also cannot return what it updated, forcing a second
/// round trip to read back the rows just claimed.
/// </para>
/// <para>
/// Each provider therefore writes this one statement by hand — claim and fetch in a single round trip, with
/// the provider's own skip-locked semantics. Everything else the outbox does stays in LINQ, where no dialect
/// differences arise.
/// </para>
/// </remarks>
internal interface IOutboxClaimStrategy {
    /// <summary>
    /// Claims up to <paramref name="batchSize"/> rows for <paramref name="workerId"/> and returns them.
    /// </summary>
    /// <param name="dbContext">The context to run against.</param>
    /// <param name="batchSize">The maximum number of rows to claim.</param>
    /// <param name="workerId">The claiming processor instance.</param>
    /// <param name="nowTicks">The current time in UTC ticks.</param>
    /// <param name="lockExpiresAtTicks">When the acquired lock expires, in UTC ticks.</param>
    /// <param name="partitionKey">When set, only rows carrying this partition key are claimed.</param>
    /// <param name="cancellationToken">A token to observe.</param>
    Task<IReadOnlyList<OutboxMessage>> ClaimAsync(
        DbContext dbContext,
        int batchSize,
        string workerId,
        long nowTicks,
        long lockExpiresAtTicks,
        string? partitionKey,
        CancellationToken cancellationToken);
}
