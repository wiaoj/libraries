using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Wrapper")]
[Trait("Feature", "TypedCounter")]
public sealed class TypedDistributedCounterWrapperTests {
  
    public sealed class TheGlobalDelegation {
        private readonly FakeCounterStorage _storage = new();
        private readonly DefaultCounterKeyBuilder _keyBuilder = new();
        private readonly DistributedCounterOptions _options = new() { DefaultStrategy = CounterStrategy.Immediate, GlobalKeyPrefix = "app:" };

        [Fact]
        public async Task GlobalIncrementAndGetValue_DelegateToInnerTagCounter() {
            // Arrange
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(this._options));
            IDistributedCounter<OrdersMetric> wrapper = new TypedDistributedCounterWrapper<OrdersMetric>(factory);

            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act
            CounterValue afterIncrement = await wrapper.IncrementAsync(5, CounterExpiry.Infinite, ct);
            CounterValue readValue = await wrapper.GetValueAsync(ct);

            // Assert
            Assert.Equal("app:OrdersMetric", wrapper.Key.Value);
            Assert.Equal(5, afterIncrement.Value);
            Assert.Equal(5, readValue.Value);
        }

        [Fact]
        public async Task GlobalTryDecrement_PassesAmountAndMinLimitInCorrectOrder() {
            // Arrange
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(this._options));
            IDistributedCounter<InventoryMetric> wrapper = new TypedDistributedCounterWrapper<InventoryMetric>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Pre-seed storage with 10 items
            await wrapper.SetAsync(10, CounterExpiry.Infinite, ct);

            // Act: Decrement 4 with minimum limit of 2 (10 - 4 = 6 >= 2, Allowed!)
            CounterLimitResult result = await wrapper.TryDecrementAsync(amount: 4, minLimit: 2, CounterExpiry.Infinite, ct);

            // Assert
            Assert.True(result.IsAllowed);
            Assert.Equal(6, result.CurrentValue);
            Assert.Equal(4, result.Remaining); // 6 - 2 = 4 capacity left
        }
    }

    public sealed class TheScopedKeyDelegation {
        private readonly FakeCounterStorage _storage = new();
        private readonly DefaultCounterKeyBuilder _keyBuilder = new();
        private readonly DistributedCounterOptions _options = new() { DefaultStrategy = CounterStrategy.Immediate, GlobalKeyPrefix = "app:" };

        [Fact]
        public async Task ForKey_CreatesDistinctCounterScopedToSpecificIdentity() {
            // Arrange
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(this._options));
            IDistributedCounter<RateLimitMetric> wrapper = new TypedDistributedCounterWrapper<RateLimitMetric>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Increment for user 101 and user 202 separately
            IDistributedCounter user101Counter = wrapper.ForKey("user_101");
            IDistributedCounter user202Counter = wrapper.ForKey("user_202");

            await user101Counter.IncrementAsync(3, CounterExpiry.Infinite, ct);
            await user202Counter.IncrementAsync(7, CounterExpiry.Infinite, ct);

            // Assert
            Assert.Equal("app:RateLimitMetric:user_101", user101Counter.Key.Value);
            Assert.Equal("app:RateLimitMetric:user_202", user202Counter.Key.Value);

            Assert.Equal(3, (await user101Counter.GetValueAsync(ct)).Value);
            Assert.Equal(7, (await user202Counter.GetValueAsync(ct)).Value);
        }

        [Fact]
        public async Task ScopedExtensionMethods_DirectlyOperateOnScopedKey() {
            // Arrange
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(this._options));
            IDistributedCounter<RateLimitMetric> wrapper = new TypedDistributedCounterWrapper<RateLimitMetric>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Using extension method wrapper.IncrementAsync(key, amount, ...)
            await wrapper.IncrementAsync("client_ip", 10, CounterExpiry.Infinite, ct);
            CounterValue val = await wrapper.GetValueAsync("client_ip", ct);

            // Assert
            Assert.Equal(10, val.Value);
        }
    }

    public sealed class TheTagBasedStrategyAndScopingBehavior {

        [Fact]
        public async Task ForKey_WhenGlobalStrategyIsBuffered_EnforcesConfiguredImmediateTagStrategy() {
            // Arrange: Global default is Buffered, but SecurityTag is explicitly registered as Immediate
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AddImmediateCounter<SecurityTag>();
            });

            IDistributedCounterFactory factory = context.CreateFactory();
            IDistributedCounter<SecurityTag> typedCounter = new TypedDistributedCounterWrapper<SecurityTag>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            string clientIp = "192.168.1.50";

            // Act: Scope the counter to a dynamic client IP
            IDistributedCounter scopedCounter = typedCounter.ForKey(clientIp);

            // Increment scoped counter by 5
            await scopedCounter.IncrementAsync(5, CounterExpiry.Infinite, ct);

            // Assert
            // 1. The scoped counter must strictly be Immediate (not falling back to default Buffered)
            Assert.Equal(CounterStrategy.Immediate, scopedCounter.Strategy);
            Assert.IsType<ImmediateDistributedCounter>(scopedCounter);

            // 2. Storage must immediately reflect the value of 5 without needing any Flush
            Assert.Equal(5, (await context.Storage.GetAsync(scopedCounter.Key, ct)).Value);
            Assert.Equal(1, context.Storage.AtomicIncrementCallCount);
        }

        [Fact]
        public async Task ForKey_AcrossDifferentTagsWithIdenticalKey_IsolatesStorageStateCompletely() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.AddImmediateCounter<AuthTag>();
                opt.AddImmediateCounter<PaymentTag>();
            });

            IDistributedCounterFactory factory = context.CreateFactory();
            IDistributedCounter<AuthTag> authCounter = new TypedDistributedCounterWrapper<AuthTag>(factory);
            IDistributedCounter<PaymentTag> paymentCounter = new TypedDistributedCounterWrapper<PaymentTag>(factory);
            CancellationToken ct = TestContext.Current.CancellationToken;

            string sharedUserId = "user_999";

            // Act: Perform operations on both tags using the exact same dynamic user ID
            await authCounter.ForKey(sharedUserId).IncrementAsync(2, CounterExpiry.Infinite, ct);
            await paymentCounter.ForKey(sharedUserId).IncrementAsync(10, CounterExpiry.Infinite, ct);

            // Assert: Storage keys and values must be completely isolated
            CounterValue authVal = await authCounter.ForKey(sharedUserId).GetValueAsync(ct);
            CounterValue paymentVal = await paymentCounter.ForKey(sharedUserId).GetValueAsync(ct);

            Assert.Equal(2, authVal.Value);
            Assert.Equal(10, paymentVal.Value);

            Assert.Equal("wiaoj:counter:AuthTag:user_999", authCounter.ForKey(sharedUserId).Key.Value);
            Assert.Equal("wiaoj:counter:PaymentTag:user_999", paymentCounter.ForKey(sharedUserId).Key.Value);
        }

        private sealed class SecurityTag;
        private sealed class AuthTag;
        private sealed class PaymentTag;
    }

    private sealed class OrdersMetric;
    private sealed class InventoryMetric;
    private sealed class RateLimitMetric;
}