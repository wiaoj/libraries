using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using System.Diagnostics;
using Wiaoj.Ddd.EntityFrameworkCore.Internal;
using Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration.Fixtures;

namespace Wiaoj.Ddd.EntityFrameworkCore.Tests.Integration;

/// <summary>
/// Polling alone puts a full interval between a commit and the handler running. The signal closes that gap:
/// a commit that enqueued rows wakes the processor immediately, and a commit that failed does not.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "Ddd")]
[Trait("Component", "Outbox")]
public sealed class OutboxSignalTests {

    private static readonly DateTimeOffset Origin = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    public sealed class TheSignalPrimitive {
        [Fact]
        public async Task Should_Return_Immediately_When_Pulsed() {
            using OutboxSignal<OutboxTestContext> signal = new();
            signal.Pulse();

            long start = Stopwatch.GetTimestamp();
            bool woken = await signal.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

            Assert.True(woken);
            Assert.True(Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task Should_Report_A_Timeout_When_Nothing_Pulses() {
            using OutboxSignal<OutboxTestContext> signal = new();

            Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Collapse_Repeated_Pulses_Into_One_Wake_Up() {
            using OutboxSignal<OutboxTestContext> signal = new();

            // A batch of commits must not queue a wake-up each; one pass drains whatever is there.
            for(int i = 0; i < 10; i++) {
                signal.Pulse();
            }

            Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
            Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken));
        }
    }

    public sealed class TheCommitHook {
        [Fact]
        public async Task Should_Pulse_When_A_Commit_Enqueued_Rows() {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = OutboxHarness.Sqlite(time);

            OutboxSignal<OutboxTestContext> signal = harness.Services.GetRequiredService<OutboxSignal<OutboxTestContext>>();

            // Nothing has committed yet.
            Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken));

            using(IServiceScope scope = harness.Services.CreateScope()) {
                OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();
                Invoice invoice = new(new InvoiceId(1), "INV-1");
                invoice.Raise(Origin);
                context.Invoices.Add(invoice);
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            Assert.True(await signal.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task Should_Not_Pulse_For_A_Save_That_Enqueued_Nothing() {
            FakeTimeProvider time = new(Origin);
            await using OutboxHarness harness = OutboxHarness.Sqlite(time);

            OutboxSignal<OutboxTestContext> signal = harness.Services.GetRequiredService<OutboxSignal<OutboxTestContext>>();

            using(IServiceScope scope = harness.Services.CreateScope()) {
                OutboxTestContext context = scope.ServiceProvider.GetRequiredService<OutboxTestContext>();
                // No domain event raised, so no rows and nothing to wake anyone for.
                context.Invoices.Add(new Invoice(new InvoiceId(1), "INV-1"));
                await context.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            Assert.False(await signal.WaitAsync(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken));
        }
    }
}
