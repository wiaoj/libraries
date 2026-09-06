using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// A context using a naming convention maps <c>Id</c> to <c>id</c> and <c>ProcessedAtTicks</c> to
/// <c>processed_at_ticks</c>. The claim statement read the table name from the model but spelled the columns
/// out by property name, so on such a context every poll failed with <c>column "Id" does not exist</c> —
/// forever, since the claim is the only way rows are ever picked up.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxNamingConventionTests {

    private static (SnakeCaseOutboxContext Context, SqliteConnection Connection) Create() {
        SqliteConnection connection = new("DataSource=:memory:");
        connection.Open();

        SnakeCaseOutboxContext context = new(
            new DbContextOptionsBuilder<SnakeCaseOutboxContext>().UseSqlite(connection).Options);

        context.Database.EnsureCreated();
        return (context, connection);
    }

    [Fact]
    public async Task Should_Claim_From_A_Context_Whose_Columns_Are_Renamed() {
        (SnakeCaseOutboxContext context, SqliteConnection connection) = Create();

        try {
            context.Set<OutboxMessage>().Add(
                OutboxMessage.Pending("orders.created.v1", "orders.notify", "{}", null, DateTimeOffset.UnixEpoch));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            IOutboxClaimStrategy strategy = new OutboxClaimStrategyFactory().Create(context);

            IReadOnlyList<OutboxMessage> claimed = await strategy.ClaimAsync(
                context, batchSize: 10, workerId: "worker-1",
                nowTicks: DateTimeOffset.UtcNow.UtcTicks,
                lockExpiresAtTicks: DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks,
                partitionKey: null,
                TestContext.Current.CancellationToken);

            OutboxMessage row = Assert.Single(claimed);
            Assert.Equal("orders.created.v1", row.EventAlias);
            Assert.Equal("orders.notify", row.HandlerAlias);
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Should_Filter_By_Partition_Against_The_Renamed_Column() {
        (SnakeCaseOutboxContext context, SqliteConnection connection) = Create();

        try {
            context.Set<OutboxMessage>().Add(
                OutboxMessage.Pending("orders.created.v1", "orders.notify", "{}", "eu-west", DateTimeOffset.UnixEpoch));
            context.Set<OutboxMessage>().Add(
                OutboxMessage.Pending("orders.created.v1", "orders.notify", "{}", "us-east", DateTimeOffset.UnixEpoch));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            IOutboxClaimStrategy strategy = new OutboxClaimStrategyFactory().Create(context);

            IReadOnlyList<OutboxMessage> claimed = await strategy.ClaimAsync(
                context, batchSize: 10, workerId: "worker-1",
                nowTicks: DateTimeOffset.UtcNow.UtcTicks,
                lockExpiresAtTicks: DateTimeOffset.UtcNow.AddMinutes(1).UtcTicks,
                partitionKey: "eu-west",
                TestContext.Current.CancellationToken);

            Assert.Equal("eu-west", Assert.Single(claimed).PartitionKey);
        }
        finally {
            await context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
