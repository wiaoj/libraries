using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.Testing;
using Xunit;

namespace Wiaoj.DistributedCounter.Tests.Unit.Internal;

[Trait("Category", "Unit")]
[Trait("Component", "Factory")]
[Trait("Feature", "CounterFactory")]
public sealed class DistributedCounterFactoryTests {

    public sealed class TheCreateOperations {
        private readonly FakeCounterStorage _storage = new();
        private readonly DefaultCounterKeyBuilder _keyBuilder = new();

        [Fact]
        public void GivenSimpleName_CreatesCounterWithCorrectKey() {
            // Arrange
            DistributedCounterOptions options = new() { GlobalKeyPrefix = "app:" };
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter = factory.Create("page_views");

            // Assert
            Assert.Equal("app:page_views", counter.Key.Value);
        }

        [Fact]
        public void GivenTagType_CreatesCounterUsingTypeName() {
            // Arrange
            DistributedCounterOptions options = new() { GlobalKeyPrefix = "app:" };
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter = factory.Create<UserLoginMarker>();

            // Assert
            Assert.Equal("app:UserLoginMarker", counter.Key.Value);
        }

        [Fact]
        public void GivenTagAndDynamicKey_CreatesCompositeCounter() {
            // Arrange
            DistributedCounterOptions options = new() { GlobalKeyPrefix = "app:" };
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter = factory.Create<UserLoginMarker, int>(42);

            // Assert
            Assert.Equal("app:UserLoginMarker:42", counter.Key.Value);
        }

        [Fact]
        public void MultipleCreateCallsWithSameKey_ReturnExactSameInstance() {
            // Arrange
            DistributedCounterOptions options = new();
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter1 = factory.Create("shared_metric");
            IDistributedCounter counter2 = factory.Create("shared_metric");

            // Assert
            Assert.Same(counter1, counter2);
        }

        [Fact]
        public async Task ConcurrentCreateCallsWithSameKey_AlwaysReturnExactSameInstance() {
            // Arrange
            DistributedCounterOptions options = new();
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            const int concurrency = 100;
            IDistributedCounter?[] results = new IDistributedCounter?[concurrency];

            // Act
            await Task.WhenAll(Enumerable.Range(0, concurrency).Select(i => Task.Run(() => {
                results[i] = factory.Create("racy_shared_counter");
            })));

            // Assert
            IDistributedCounter first = results[0]!;
            Assert.All(results, r => Assert.Same(first, r));
        }
        private sealed class UserLoginMarker;
    }

    public sealed class TheStrategyResolution {
        private readonly FakeCounterStorage _storage = new();
        private readonly DefaultCounterKeyBuilder _keyBuilder = new();

        [Fact]
        public void RegisteredImmediateStrategy_InstantiatesImmediateCounter() {
            // Arrange
            DistributedCounterOptions options = new() {
                DefaultStrategy = CounterStrategy.Buffered
            };
            options.AddCounter("critical_quota", CounterStrategy.Immediate);

            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter = factory.Create("critical_quota");

            // Assert
            Assert.Equal(CounterStrategy.Immediate, counter.Strategy);
            Assert.IsType<ImmediateDistributedCounter>(counter);
        }

        [Fact]
        public void UnregisteredCounter_FallsBackToDefaultStrategy() {
            // Arrange
            DistributedCounterOptions options = new() {
                DefaultStrategy = CounterStrategy.Buffered
            };

            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));

            // Act
            IDistributedCounter counter = factory.Create("standard_metric");

            // Assert
            Assert.Equal(CounterStrategy.Buffered, counter.Strategy);
            Assert.IsType<BufferedDistributedCounter>(counter);
        }
    }

    public sealed class TheBufferedCounterSourceTracking {
        private readonly FakeCounterStorage _storage = new();
        private readonly DefaultCounterKeyBuilder _keyBuilder = new();

        [Fact]
        public void FactoryTracksOnlyBufferedCountersInBufferedSource() {
            // Arrange
            DistributedCounterOptions options = new() { DefaultStrategy = CounterStrategy.Buffered };
            options.AddCounter("immediate_counter", CounterStrategy.Immediate);

            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));
            IBufferedCounterSource source = factory;

            // Act
            factory.Create("buffered_1");
            factory.Create("buffered_2");
            factory.Create("immediate_counter"); // Should not be in buffered list

            // Assert
            var bufferedList = source.GetBufferedCounters().ToArray();
            var allTracked = source.GetAllTrackedCounters().ToArray();

            Assert.Equal(2, bufferedList.Length);
            Assert.Equal(3, allTracked.Length);
        }

        [Fact]
        public void ClearCache_EmptiesAllTrackedAndBufferedCounters() {
            // Arrange
            DistributedCounterOptions options = new() { DefaultStrategy = CounterStrategy.Buffered };
            DistributedCounterFactory factory = new(this._storage, this._keyBuilder, Options.Create(options));
            IBufferedCounterSource source = factory;

            factory.Create("c1");
            factory.Create("c2");

            // Act
            source.ClearCache();

            // Assert
            Assert.Empty(source.GetBufferedCounters());
            Assert.Empty(source.GetAllTrackedCounters());
        }
    }

    public sealed class ThePolicyAndCategoryScoping {

        [Fact]
        public void GivenNamedPolicyWithImmediateStrategy_AndDynamicKey_ResolvesImmediateCounterInsteadOfDefaultBuffered() {
            // Arrange: Global default is Buffered, but "AuthPolicy" is explicitly registered as Immediate
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AddImmediateCounter("AuthPolicy");
            });

            IDistributedCounterFactory factory = context.CreateFactory();

            // Act 1: Create counter under the "AuthPolicy" category for a specific IP
            IDistributedCounter authCounter = factory.Create("AuthPolicy", "192.168.1.50");

            // Act 2: Create another counter under an unregistered category for the same IP (falls back to default)
            IDistributedCounter searchCounter = factory.Create("SearchPolicy", "192.168.1.50");

            // Assert
            // Auth counter must strictly enforce Immediate strategy
            Assert.Equal(CounterStrategy.Immediate, authCounter.Strategy);
            Assert.IsType<ImmediateDistributedCounter>(authCounter);
            Assert.Equal("wiaoj:counter:AuthPolicy:192.168.1.50", authCounter.Key.Value);

            // Search counter falls back to the default Buffered strategy
            Assert.Equal(CounterStrategy.Buffered, searchCounter.Strategy);
            Assert.IsType<BufferedDistributedCounter>(searchCounter);
            Assert.Equal("wiaoj:counter:SearchPolicy:192.168.1.50", searchCounter.Key.Value);
        }

        [Fact]
        public async Task GivenSameDynamicKeyAcrossDifferentPolicies_IsolatesStateAndKeysCompletely() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.AddImmediateCounter("LoginPolicy");
                opt.AddImmediateCounter("OrderPolicy");
            });

            IDistributedCounterFactory factory = context.CreateFactory();
            CancellationToken ct = TestContext.Current.CancellationToken;

            string clientIp = "192.168.1.100";

            // Act: Resolve two different policy counters for the exact same IP
            IDistributedCounter loginCounter = factory.Create("LoginPolicy", clientIp);
            IDistributedCounter orderCounter = factory.Create("OrderPolicy", clientIp);

            // Increment login counter by 3, and order counter by 10
            await loginCounter.IncrementAsync(3, CounterExpiry.Infinite, ct);
            await orderCounter.IncrementAsync(10, CounterExpiry.Infinite, ct);

            // Assert: The two counters must be completely isolated in keys and values
            Assert.NotEqual(loginCounter.Key, orderCounter.Key);
            Assert.Equal("wiaoj:counter:LoginPolicy:192.168.1.100", loginCounter.Key.Value);
            Assert.Equal("wiaoj:counter:OrderPolicy:192.168.1.100", orderCounter.Key.Value);

            Assert.Equal(3, (await loginCounter.GetValueAsync(ct)).Value);
            Assert.Equal(10, (await orderCounter.GetValueAsync(ct)).Value);
        }

        [Fact]
        public void GivenTypedTagPolicy_WithDynamicKey_EnforcesConfiguredTagStrategy() {
            // Arrange
            DistributedCounterTestContext context = new(opt => {
                opt.DefaultStrategy = CounterStrategy.Buffered;
                opt.AddImmediateCounter<SecurityMarkerTag>();
            });

            IDistributedCounterFactory factory = context.CreateFactory();

            // Act: Create counter using generic Tag + dynamic user ID
            IDistributedCounter userSecurityCounter = factory.Create<SecurityMarkerTag, int>(999);

            // Assert
            Assert.Equal(CounterStrategy.Immediate, userSecurityCounter.Strategy);
            Assert.IsType<ImmediateDistributedCounter>(userSecurityCounter);
            Assert.Equal("wiaoj:counter:SecurityMarkerTag:999", userSecurityCounter.Key.Value);
        }

        private sealed class SecurityMarkerTag;
    }
}