using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Idempotency;

namespace Wiaoj.Webhooks.Tests.Unit.Idempotency;

[Trait("Category", "Unit")]
[Trait("Feature", "Idempotency")]
[Trait("Component", "Store")]
public sealed class InMemoryIdempotencyStoreTests {

    public sealed class TheTryMarkProcessedAsyncMethod {
        [Fact]
        public async Task TryMarkProcessedAsync_ReturnsTrueFirstTime_AndFalseForDuplicatesWithinWindow() {
            FakeTimeProvider timeProvider = new();
            InMemoryIdempotencyStore store = new(timeProvider);
            IdempotencyKey key = new("idemp:ep:ev:1");
            TimeSpan window = TimeSpan.FromMinutes(10);

            // 1st attempt: First time seen -> Allowed
            bool firstAttempt = await store.TryMarkProcessedAsync(key, window, TestContext.Current.CancellationToken);
            Assert.True(firstAttempt);

            // 2nd attempt within window -> Suppressed (Duplicate)
            bool secondAttempt = await store.TryMarkProcessedAsync(key, window, TestContext.Current.CancellationToken);
            Assert.False(secondAttempt);
        }

        [Fact]
        public async Task TryMarkProcessedAsync_AllowsEventAgain_AfterWindowExpires() {
            FakeTimeProvider timeProvider = new();
            InMemoryIdempotencyStore store = new(timeProvider);
            IdempotencyKey key = new("idemp:ep:ev:2");
            TimeSpan window = TimeSpan.FromMinutes(5);

            await store.TryMarkProcessedAsync(key, window, TestContext.Current.CancellationToken);

            // Advance time past the 5-minute window
            timeProvider.Advance(TimeSpan.FromMinutes(6));

            bool afterExpiryAttempt = await store.TryMarkProcessedAsync(key, window, TestContext.Current.CancellationToken);
            Assert.True(afterExpiryAttempt);
        }

        [Fact]
        public async Task TryMarkProcessedAsync_Throws_WhenParametersInvalid() {
            InMemoryIdempotencyStore store = new();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                store.TryMarkProcessedAsync(new IdempotencyKey(""), TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken).AsTask());

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() =>
                store.TryMarkProcessedAsync(new IdempotencyKey("valid"), TimeSpan.Zero, TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheSweepExpiredMethod {
        [Fact]
        public async Task SweepExpired_RemovesOnlyExpiredEntriesFromMemory() {
            FakeTimeProvider timeProvider = new();
            InMemoryIdempotencyStore store = new(timeProvider);

            IdempotencyKey shortKey = new("idemp:short");
            IdempotencyKey longKey = new("idemp:long");

            await store.TryMarkProcessedAsync(shortKey, TimeSpan.FromMinutes(2), TestContext.Current.CancellationToken);
            await store.TryMarkProcessedAsync(longKey, TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken);

            // Advance time by 3 minutes -> shortKey expired, longKey active
            timeProvider.Advance(TimeSpan.FromMinutes(3));

            int removed = store.SweepExpired();
            Assert.Equal(1, removed);

            // shortKey can be registered fresh again
            bool shortReRegistered = await store.TryMarkProcessedAsync(shortKey, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
            Assert.True(shortReRegistered);

            // longKey is still duplicate
            bool longDuplicate = await store.TryMarkProcessedAsync(longKey, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
            Assert.False(longDuplicate);
        }
    }
}