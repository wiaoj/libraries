using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Internal.Claim;
using Wiaoj.Ddd.EntityFrameworkCore.Outbox;
using Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;
using Wiaoj.Serialization;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

// -----------------------------------------------------------------------------------------------
// The outbox writes one row per (event, handler) so a failing handler is retried alone; timestamps are
// UTC ticks so the claim query translates on every provider; and the claim itself is one provider-specific
// statement, because it is the one query LINQ cannot express.
// -----------------------------------------------------------------------------------------------
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxTests {

    private static readonly DateTimeOffset Origin = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<string> Providers => ["sqlite", "inmemory"];

    private static OutboxHarness CreateHarness(string provider, TimeProvider timeProvider) {
        return provider == "sqlite" ? OutboxHarness.Sqlite(timeProvider) : OutboxHarness.InMemory(timeProvider);
    }

    private static OutboxProcessor<OutboxTestContext> CreateProcessor(OutboxHarness harness, OutboxOptions options) {
        return new OutboxProcessor<OutboxTestContext>(
            harness.Services,
            new StaticOptionsMonitor<OutboxOptions>(options),
            harness.Services.GetRequiredService<ISerializer<DddEfCoreOutboxSerializerKey>>(),
            NullLogger<OutboxProcessor<OutboxTestContext>>.Instance,
            new OutboxInstanceInfo("worker-1"),
            new OutboxClaimStrategyFactory(),
            harness.Services.GetRequiredService<IOutboxAliasRegistry>(),
            harness.Services.GetRequiredService<OutboxHandlerCatalog>(),
            harness.Services.GetRequiredService<OutboxSignal<OutboxTestContext>>());
    }

    private static async Task RaiseInvoiceAsync(OutboxHarness harness, long id) {
        using IServiceScope scope = harness.Services.CreateScope();
        OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();

        Invoice invoice = new(new InvoiceId(id), $"INV-{id}");
        invoice.Raise(Origin);
        context.Invoices.Add(invoice);

        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<List<OutboxMessage>> ReadRowsAsync(OutboxHarness harness) {
        using IServiceScope scope = harness.Services.CreateScope();
        OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();

        return await context.Set<OutboxMessage>().AsNoTracking()
            .OrderBy(m => m.HandlerAlias)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    public sealed class TheFanOut {
        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Write_One_Row_Per_Handler(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);

            await RaiseInvoiceAsync(harness, 1);

            List<OutboxMessage> rows = await ReadRowsAsync(harness);

            Assert.Equal(2, rows.Count);
            Assert.Equal(["invoices.ledger", "invoices.notify"], rows.Select(r => r.HandlerAlias));
            Assert.All(rows, r => Assert.Equal("invoices.raised.v1", r.EventAlias));
            Assert.All(rows, r => Assert.Null(r.ProcessedAtTicks));
        }

        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Persist_The_Rows_In_The_Same_Transaction_As_The_Aggregate(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);

            using(IServiceScope scope = harness.Services.CreateScope()) {
                OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();

                Invoice invoice = new(new InvoiceId(1), "INV-1");
                invoice.Raise(Origin);
                context.Invoices.Add(invoice);

                // Rows exist only after the save that also wrote the aggregate.
                Assert.Empty(await ReadRowsAsync(harness));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(2, (await ReadRowsAsync(harness)).Count);
        }
    }

    public sealed class TheDispatch {
        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Run_Every_Handler_And_Mark_Each_Row_Processed(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);
            await RaiseInvoiceAsync(harness, 1);

            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, new OutboxOptions());
            int claimed = await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(2, claimed);
            Assert.Equal(1, harness.Log.CountOf("ledger"));
            Assert.Equal(1, harness.Log.CountOf("notify"));
            Assert.All(await ReadRowsAsync(harness), r => Assert.NotNull(r.ProcessedAtTicks));
        }

        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Not_Replay_A_Succeeding_Handler_When_A_Sibling_Fails(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);
            harness.Log.AlwaysFailing = "notify";

            await RaiseInvoiceAsync(harness, 1);

            OutboxOptions options = new();
            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, options);

            await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            // Second pass, after the backoff has elapsed: only the failing row is claimable.
            time.Advance(options.RetryPolicy.MaxDelay);
            await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            // This is the whole point of a row per handler.
            Assert.Equal(1, harness.Log.CountOf("ledger"));
            Assert.Equal(2, harness.Log.CountOf("notify"));

            List<OutboxMessage> rows = await ReadRowsAsync(harness);
            Assert.NotNull(rows.Single(r => r.HandlerAlias == "invoices.ledger").ProcessedAtTicks);
            Assert.Null(rows.Single(r => r.HandlerAlias == "invoices.notify").ProcessedAtTicks);
        }
    }

    public sealed class TheRetryAndDeadLetter {
        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Not_Reclaim_A_Failed_Row_Before_Its_Backoff_Elapses(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);
            harness.Log.AlwaysFailing = "notify";

            await RaiseInvoiceAsync(harness, 1);

            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, new OutboxOptions());
            await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(1, harness.Log.CountOf("notify"));

            // No time has passed, so the row is still serving its backoff.
            int claimed = await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            Assert.Equal(0, claimed);
            Assert.Equal(1, harness.Log.CountOf("notify"));
        }

        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Dead_Letter_A_Row_Once_Its_Attempts_Run_Out(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);
            harness.Log.AlwaysFailing = "notify";

            OutboxOptions options = new() { RetryPolicy = new OutboxRetryPolicy { MaxAttempts = 3 } };
            await RaiseInvoiceAsync(harness, 1);

            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, options);

            for(int attempt = 0; attempt < 3; attempt++) {
                await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);
                time.Advance(options.RetryPolicy.MaxDelay);
            }

            OutboxMessage failed = (await ReadRowsAsync(harness)).Single(r => r.HandlerAlias == "invoices.notify");

            Assert.NotNull(failed.DeadLetteredAtTicks);
            Assert.Equal(3, failed.Attempts);
            Assert.NotNull(failed.LastError);

            // Terminal means terminal: no further claim, however long we wait.
            time.Advance(TimeSpan.FromDays(1));
            Assert.Equal(0, await processor.ProcessBatchAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, harness.Log.CountOf("notify"));
        }

        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Dead_Letter_Rather_Than_Retry_When_The_Handler_No_Longer_Exists(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);

            await RaiseInvoiceAsync(harness, 1);

            // Rewrite one row to name a handler nothing registers, as a removed handler would leave behind.
            using(IServiceScope scope = harness.Services.CreateScope()) {
                OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();
                OutboxMessage row = await context.Set<OutboxMessage>()
                    .FirstAsync(m => m.HandlerAlias == "invoices.notify", TestContext.Current.CancellationToken);

                context.Entry(row).Property(nameof(OutboxMessage.HandlerAlias)).CurrentValue = "invoices.deleted-handler";
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, new OutboxOptions());
            await processor.ProcessBatchAsync(TestContext.Current.CancellationToken);

            OutboxMessage orphan = (await ReadRowsAsync(harness)).Single(r => r.HandlerAlias == "invoices.deleted-handler");

            Assert.NotNull(orphan.DeadLetteredAtTicks);
            Assert.Contains("no longer registered", orphan.LastError, StringComparison.Ordinal);
        }
    }

    public sealed class TheClaim {
        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Respect_The_Batch_Size(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);

            for(long id = 1; id <= 5; id++) {
                await RaiseInvoiceAsync(harness, id);
            }

            // 5 invoices x 2 handlers = 10 rows; a batch of 3 must claim exactly 3.
            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, new OutboxOptions { BatchSize = 3 });

            Assert.Equal(3, await processor.ProcessBatchAsync(TestContext.Current.CancellationToken));
            Assert.Equal(3, harness.Log.Calls.Count);
        }

        [Theory]
        [MemberData(nameof(Providers), MemberType = typeof(OutboxTests))]
        public async Task Should_Claim_Nothing_When_The_Outbox_Is_Empty(string provider) {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = CreateHarness(provider, time);

            OutboxProcessor<OutboxTestContext> processor = CreateProcessor(harness, new OutboxOptions());

            Assert.Equal(0, await processor.ProcessBatchAsync(TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Refuse_A_Provider_It_Has_No_Claim_Statement_For() {
            IOutboxClaimStrategy strategy = new UnsupportedOutboxClaimStrategy("Some.Exotic.Provider");

            NotSupportedException failure = await Assert.ThrowsAsync<NotSupportedException>(() =>
                strategy.ClaimAsync(null!, 1, "w", 0, 0, null, TestContext.Current.CancellationToken));

            Assert.Contains("Some.Exotic.Provider", failure.Message, StringComparison.Ordinal);
        }
    }
}

/// <summary>Minimal <see cref="IOptionsMonitor{T}"/> over a fixed value.</summary>
internal sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T> {
    public T CurrentValue => value;
    public T Get(string? name) => value;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
