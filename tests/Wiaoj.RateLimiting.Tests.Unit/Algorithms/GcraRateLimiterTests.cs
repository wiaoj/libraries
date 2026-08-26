using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Algorithms;

[Trait("Category", "Unit")]
[Trait("Component", "RateLimiting")]
[Trait("Feature", "Gcra")]
public sealed class GcraRateLimiterTests {

    public sealed class TheConstructorValidation {

        [Fact]
        public void GivenNullFactory_ThrowsArgumentNullException() {
            Assert.ThrowsAny<ArgumentNullException>(() => new GcraRateLimiter(null!, "policy", 10, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void GivenNullOrWhitespacePolicyName_ThrowsArgumentException(string? invalidPolicy) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentException>(() => new GcraRateLimiter(context.Factory, invalidPolicy!, 10, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativeLimit_ThrowsArgumentOutOfRangeException(int invalidLimit) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new GcraRateLimiter(context.Factory, "policy", invalidLimit, TimeSpan.FromSeconds(1)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GivenZeroOrNegativePeriod_ThrowsArgumentOutOfRangeException(long invalidTicks) {
            DistributedCounterTestContext context = new();
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new GcraRateLimiter(context.Factory, "policy", 10, TimeSpan.FromTicks(invalidTicks)));
        }
    }

    public sealed class TheTryAcquireAsyncArgumentValidation {

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task GivenZeroOrNegativeCost_ThrowsArgumentOutOfRangeException(int invalidCost) {
            // Arrange
            DistributedCounterTestContext context = new();
            GcraRateLimiter limiter = new(context.Factory, "gcra_cost_validation", limit: 10, period: TimeSpan.FromSeconds(1));
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(
                () => limiter.TryAcquireAsync("client_invalid_cost", invalidCost, ct).AsTask());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task GivenNullOrEmptyKey_ThrowsArgumentException(string? invalidKey) {
            // Arrange
            DistributedCounterTestContext context = new();
            GcraRateLimiter limiter = new(context.Factory, "gcra_key_validation", limit: 10, period: TimeSpan.FromSeconds(1));
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(
                () => limiter.TryAcquireAsync(invalidKey!, cost: 1, ct).AsTask());
        }
    }

    public sealed class TheBurstAndReplenishmentMath {

        [Fact]
        public async Task IdleKey_CanAbsorbFullBurstInstantly() {
            // Arrange: 5 tokens per 5 seconds (1 token per second emission interval)
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            DistributedCounterTestContext context = new(timeProvider);
            GcraRateLimiter limiter = new(context.Factory, "gcra_burst", limit: 5, period: TimeSpan.FromSeconds(5), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_burst";

            // Act 1: Consume full burst of 5 immediately
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 5, ct);

            // Assert 1
            Assert.True(d1.IsAllowed);
            Assert.Equal(0, d1.Remaining); // All 5 consumed, TAT is projected +5s into future

            // Act 2: Immediate next request must be denied
            RateLimitDecision d2 = await limiter.TryAcquireAsync(key, cost: 1, ct);

            // Assert 2
            Assert.False(d2.IsAllowed);
            Assert.NotNull(d2.RetryAfter);
            Assert.Equal(1, (int)Math.Ceiling(d2.RetryAfter.Value.TotalSeconds)); // 1s wait for 1 token to regenerate
        }

        [Fact]
        public async Task DrainedKey_RegeneratesTokensAtConstantRate() {
            // Arrange: 10 tokens per 10 seconds (1 token per second)
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            DistributedCounterTestContext context = new(timeProvider);
            GcraRateLimiter limiter = new(context.Factory, "gcra_regen", limit: 10, period: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_regen";

            // Drain all 10 tokens
            await limiter.TryAcquireAsync(key, cost: 10, ct);

            // Advance time by 3 seconds (recovers 3 tokens)
            timeProvider.Advance(TimeSpan.FromSeconds(3));

            // Act: Request 3 tokens
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 3, ct);

            // Assert
            Assert.True(d1.IsAllowed);
            Assert.Equal(0, d1.Remaining);

            // Request 1 more immediately -> must be denied because only 3 were recovered
            RateLimitDecision d2 = await limiter.TryAcquireAsync(key, cost: 1, ct);
            Assert.False(d2.IsAllowed);
        }

        [Fact]
        public async Task RequestWithCostGreaterThanLimit_IsDeniedImmediatelyWithoutAlteringState() {
            // Arrange: Max burst is 5
            DistributedCounterTestContext context = new();
            GcraRateLimiter limiter = new(context.Factory, "gcra_overflow", limit: 5, period: TimeSpan.FromSeconds(5));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_overflow";

            // Act: Request 6 units
            RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 6, ct);

            // Assert
            Assert.False(decision.IsAllowed);
            Assert.Equal(5, decision.Remaining); // Full limit untouched

            // Counter in storage must not exist/be 0
            IDistributedCounter counter = context.Factory.Create("gcra_overflow", key);
            Assert.Equal(0, (await counter.GetValueAsync(ct)).Value);
        }

        [Fact]
        public async Task NonEvenlyDivisibleLimitAndPeriod_UsesFlooredEmissionIntervalConsistently() {
            // Arrange: 3 tokens per 1 second -> 10_000_000 ticks / 3 does NOT divide evenly.
            // emissionIntervalTicks = floor(10_000_000 / 3) = 3_333_333 ticks (333.3333 ms)
            // burstToleranceTicks   = 3_333_333 * 3 = 9_999_999 ticks (1 tick short of the full 1s period)
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            DistributedCounterTestContext context = new(timeProvider);
            GcraRateLimiter limiter = new(context.Factory, "gcra_truncation", limit: 3, period: TimeSpan.FromSeconds(1), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_truncation";

            // Act 1: Drain the full burst of 3 at t=0
            RateLimitDecision d1 = await limiter.TryAcquireAsync(key, cost: 3, ct);
            Assert.True(d1.IsAllowed);
            Assert.Equal(0, d1.Remaining);

            // Act 2: Immediately request 1 more -> must be denied.
            // Expected wait is exactly one floored emission interval (3_333_333 ticks),
            // NOT a naive double-based 333ms or 334ms rounding.
            RateLimitDecision d2 = await limiter.TryAcquireAsync(key, cost: 1, ct);
            TimeSpan expectedRetryAfter = TimeSpan.FromTicks(3_333_333);

            Assert.False(d2.IsAllowed);
            Assert.Equal(0, d2.Remaining);
            Assert.NotNull(d2.RetryAfter);
            Assert.Equal(expectedRetryAfter, d2.RetryAfter!.Value);

            // Act 3: Advance time by exactly that emission interval -> request must now succeed,
            // proving the floored tick math is self-consistent across the deny/allow boundary.
            timeProvider.Advance(expectedRetryAfter);
            RateLimitDecision d3 = await limiter.TryAcquireAsync(key, cost: 1, ct);

            Assert.True(d3.IsAllowed);
        }
    }

    public sealed class TheOptimisticCasConcurrency {

        [Fact]
        public async Task ConcurrentAcquisitions_UnderContention_EnforceStrictLimitWithoutTokenLeak() {
            // Arrange: Limit of 20 requests per 10 seconds
            DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            FakeTimeProvider timeProvider = new(now);
            DistributedCounterTestContext context = new(timeProvider);
            GcraRateLimiter limiter = new(context.Factory, "gcra_concurrency", limit: 20, period: TimeSpan.FromSeconds(10), timeProvider: timeProvider);
            CancellationToken ct = TestContext.Current.CancellationToken;
            string key = "client_race";

            const int totalAttempts = 50;
            int allowedCount = 0;
            int deniedCount = 0;

            // Act: 50 threads racing to acquire 1 token simultaneously
            Task[] tasks = [.. Enumerable.Range(0, totalAttempts)
                .Select(_ => Task.Run(async () => {
                    RateLimitDecision decision = await limiter.TryAcquireAsync(key, cost: 1, ct);
                    if(decision.IsAllowed) {
                        Interlocked.Increment(ref allowedCount);
                    }
                    else {
                        Interlocked.Increment(ref deniedCount);
                    }
                }, ct))];

            await Task.WhenAll(tasks);

            // Assert: Exactly 20 allowed (full burst capacity) and 30 denied. Zero lost updates or race breaches.
            Assert.Equal(20, allowedCount);
            Assert.Equal(30, deniedCount);
        }
    }

    public sealed class TheCancellationBehavior {

        [Fact]
        public async Task GivenAlreadyCancelledToken_ThrowsOperationCanceledException() {
            // Arrange
            DistributedCounterTestContext context = new();
            GcraRateLimiter limiter = new(context.Factory, "gcra_precancelled", limit: 10, period: TimeSpan.FromSeconds(1));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            // Act & Assert: ThrowIfCancellationRequested() fires before the retry loop or any storage access
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => limiter.TryAcquireAsync("client_precancelled", cost: 1, cts.Token).AsTask());
        }

        [Fact]
        public async Task CancellationRequested_WhileRetryingUnderContention_ReturnsDeniedWithoutThrowing() {
            // Arrange: a storage double whose CAS always fails (simulating perpetual contention)
            // and which cancels the token on invocation, forcing the retry loop's while-condition
            // to exit gracefully instead of throwing or looping forever.
            ServiceCollection services = new();
            using CancellationTokenSource cts = new();
            AlwaysContendedCounterStorage storage = new(cts);

            services.AddDistributedCounter(dc => {
                dc.Services.AddSingleton<ICounterStorage>(storage);
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IDistributedCounterFactory factory = provider.GetRequiredService<IDistributedCounterFactory>();

            GcraRateLimiter limiter = new(factory, "gcra_cancel_midloop", limit: 5, period: TimeSpan.FromSeconds(5));

            // Act
            RateLimitDecision decision = await limiter.TryAcquireAsync("client_cancel_midloop", cost: 1, cts.Token);

            // Assert: falls through to the final fallback Denied(period, remaining:0) without throwing
            Assert.False(decision.IsAllowed);
            Assert.Equal(0, decision.Remaining);
            Assert.Equal(TimeSpan.FromSeconds(5), decision.RetryAfter);
        }

        private sealed class AlwaysContendedCounterStorage(CancellationTokenSource cts) : ICounterStorage {
            public ValueTask<CounterValue> GetAsync(CounterKey key, CancellationToken cancellationToken)
                => new(CounterValue.Zero);

            public ValueTask<bool> TryCompareExchangeAsync(CounterKey key, CounterValue expectedValue, CounterValue newValue, CounterExpiry expiry, CancellationToken cancellationToken) {
                cts.Cancel(); // Cancellation arrives while the CAS attempt is in flight
                return new ValueTask<bool>(false);
            }

            // GcraRateLimiter never touches these members; fail loudly if that assumption ever changes.
            public ValueTask<CounterValue> AtomicIncrementAsync(CounterKey key, long amount, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask DeleteAsync(CounterKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask SetAsync(CounterKey key, CounterValue value, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<CounterLimitResult> TryIncrementAsync(CounterKey key, long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<CounterLimitResult> TryDecrementAsync(CounterKey key, long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<TimeSpan?> GetTtlAsync(CounterKey key, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask<IDictionary<CounterKey, CounterValue>> GetManyAsync(IEnumerable<CounterKey> keys, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask GetManyAsync(ReadOnlyMemory<CounterKey> keys, Memory<CounterValue> destination, CancellationToken cancellationToken) => throw new NotSupportedException();
            public ValueTask BatchIncrementAsync(ReadOnlyMemory<CounterUpdate> updates, Memory<long> resultDestination, CancellationToken cancellationToken) => throw new NotSupportedException();
        }
    }

    public sealed class ThePolicyAndKeyIsolation {

        [Fact]
        public async Task SameKeyAcrossDifferentPolicies_OperatesWithIndependentTatState() {
            // Arrange
            DistributedCounterTestContext context = new();
            GcraRateLimiter policyA = new(context.Factory, "gcra_policy_a", limit: 5, period: TimeSpan.FromSeconds(5));
            GcraRateLimiter policyB = new(context.Factory, "gcra_policy_b", limit: 10, period: TimeSpan.FromSeconds(10));
            CancellationToken ct = TestContext.Current.CancellationToken;
            string clientIp = "172.16.0.1";

            // Act: Drain policy A
            await policyA.TryAcquireAsync(clientIp, cost: 5, ct);
            RateLimitDecision aDenied = await policyA.TryAcquireAsync(clientIp, cost: 1, ct);

            // Act: Policy B should be fully intact
            RateLimitDecision bAllowed = await policyB.TryAcquireAsync(clientIp, cost: 5, ct);

            // Assert
            Assert.False(aDenied.IsAllowed);
            Assert.True(bAllowed.IsAllowed);
            Assert.Equal(5, bAllowed.Remaining);
        }
    }
}