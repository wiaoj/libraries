using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Wiaoj.DistributedCounter.Hosting;
using Wiaoj.DistributedCounter.Internal;
using Wiaoj.DistributedCounter.DependencyInjection;

namespace Wiaoj.DistributedCounter.Tests.Unit.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Feature", "ServiceRegistration")]
public sealed class DistributedCounterServiceCollectionExtensionsTests {

    public sealed class TheServiceRegistration {

        [Fact]
        public void AddDistributedCounter_RegistersCoreServicesWithExpectedLifetimes() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddDistributedCounter()
                    .UseInMemory();

            using ServiceProvider provider = services.BuildServiceProvider();

            // Assert: Singletons
            Assert.NotNull(provider.GetService<ICounterStorage>());
            Assert.NotNull(provider.GetService<ICounterKeyBuilder>());
            Assert.NotNull(provider.GetService<IDistributedCounterFactory>());
            Assert.NotNull(provider.GetService<IDistributedCounterService>());

            IDistributedCounterFactory f1 = provider.GetRequiredService<IDistributedCounterFactory>();
            IDistributedCounterFactory f2 = provider.GetRequiredService<IDistributedCounterFactory>();
            Assert.Same(f1, f2); // Factory is Singleton

            // Assert: Open-generic wrapper is Transient
            IDistributedCounter<TestMetricTag> w1 = provider.GetRequiredService<IDistributedCounter<TestMetricTag>>();
            IDistributedCounter<TestMetricTag> w2 = provider.GetRequiredService<IDistributedCounter<TestMetricTag>>();
            Assert.NotSame(w1, w2); // Wrapper itself is transient
            Assert.Equal(w1.Key, w2.Key); // But points to same underlying key
        }

        [Fact]
        public void AddAutoFlush_RegistersCounterAutoFlushServiceAsHostedService() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddDistributedCounter()
                    .UseInMemory()
                    .AddAutoFlush();

            using ServiceProvider provider = services.BuildServiceProvider();

            // Assert
            var hostedServices = provider.GetServices<IHostedService>();
            Assert.Contains(hostedServices, s => s is CounterAutoFlushService);
        }
    }

    public sealed class TheFluentCounterRegistrations {

        [Fact]
        public void BuilderExtensions_ConfigureOptionsCorrectly() {
            // Arrange
            ServiceCollection services = new();

            // Act
            services.AddDistributedCounter(builder => {
                builder.Configure(opt => opt.GlobalKeyPrefix = "custom:")
                       .AddImmediateCounter<CircuitBreakerTag>()
                       .AddBufferedCounter("page_views")
                       .AddCounter<RateLimitTag>(cfg => cfg.Strategy = CounterStrategy.Immediate);

                builder.UseInMemory();
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            DistributedCounterOptions options = provider.GetRequiredService<IOptions<DistributedCounterOptions>>().Value;

            // Assert
            Assert.Equal("custom:", options.GlobalKeyPrefix);
            Assert.Equal(3, options.Registrations.Count);

            Assert.Equal(CounterStrategy.Immediate, options.Registrations[nameof(CircuitBreakerTag)].Strategy);
            Assert.Equal(CounterStrategy.Buffered, options.Registrations["page_views"].Strategy);
            Assert.Equal(CounterStrategy.Immediate, options.Registrations[nameof(RateLimitTag)].Strategy);
        }

        [Fact]
        public void FactoryResolvesConfiguredStrategiesFromBuilderCorrectly() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(builder => {
                builder.AddImmediateCounter<CircuitBreakerTag>()
                       .AddBufferedCounter("page_views");

                builder.UseInMemory();
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IDistributedCounterFactory factory = provider.GetRequiredService<IDistributedCounterFactory>();

            // Act
            IDistributedCounter immediateCounter = factory.Create<CircuitBreakerTag>();
            IDistributedCounter bufferedCounter = factory.Create("page_views");

            // Assert
            Assert.Equal(CounterStrategy.Immediate, immediateCounter.Strategy);
            Assert.IsType<ImmediateDistributedCounter>(immediateCounter);

            Assert.Equal(CounterStrategy.Buffered, bufferedCounter.Strategy);
            Assert.IsType<BufferedDistributedCounter>(bufferedCounter);
        }
    }

    private sealed class TestMetricTag;
    private sealed class CircuitBreakerTag;
    private sealed class RateLimitTag;
}