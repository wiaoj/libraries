using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience;
using Wiaoj.Webhooks.Resilience;

namespace Wiaoj.Webhooks.Tests.Unit.Resilience;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "DependencyInjection")]
public sealed class WebhookBuilderResilienceExtensionsTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ServiceProvider Build(Action<IWebhookBuilder> configure, out FakeTimeProvider time) {
        time = new FakeTimeProvider();
        ServiceCollection services = new();
        services.AddSingleton<TimeProvider>(time);
        services.AddDistributedCounter(dc => dc.UseInMemory());
        IWebhookBuilder builder = services.AddWiaojWebhooks();
        configure(builder);
        return services.BuildServiceProvider();
    }

    private static async Task FailAsync(ICircuitBreaker breaker, string key, int times) {
        for(int i = 0; i < times; i++) {
            Assert.True((await breaker.TryAcquireAsync(key, Ct)).IsAllowed);
            await breaker.OnFailureAsync(key, Ct);
        }
    }

    public sealed class TheConsecutiveBreaker {
        [Fact]
        public void AddsTheBreakerMiddlewareToThePipeline() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseCircuitBreaker(), out _);

            Assert.Contains(provider.GetServices<IWebhookMiddleware>(), middleware => middleware is CircuitBreakerMiddleware);
        }

        [Fact]
        public async Task TheConfiguredThresholdAndBreakDurationReachTheBreaker() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseCircuitBreaker(options => {
                options.FailureThreshold = 2;
                options.BreakDuration = TimeSpan.FromSeconds(10);
            }), out FakeTimeProvider time);
            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreaker>();

            await FailAsync(breaker, "ep", 2);

            Assert.False((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);
            time.Advance(TimeSpan.FromSeconds(11));
            Assert.True((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);
        }

        [Fact]
        public async Task TheDefaultsLeaveRoomBeforeTheCircuitOpens() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseCircuitBreaker(), out _);
            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreaker>();

            await FailAsync(breaker, "ep", 1);

            Assert.True((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);
        }

        [Fact]
        public void InvalidOptionsFailWhenTheyAreRegistered_NotWhenTheFirstWebhookIsSent() {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.UseCircuitBreaker(options => options.BreakDuration = TimeSpan.Zero));
        }

        [Fact]
        public void ANullBuilderOrNullOptionsIsRefused() {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.ThrowsAny<ArgumentNullException>(() => WebhookBuilderResilienceExtensions.UseCircuitBreaker(null!));
            Assert.ThrowsAny<ArgumentNullException>(() => builder.UseCircuitBreaker((Action<CircuitBreakerOptions>)null!));
            Assert.ThrowsAny<ArgumentNullException>(() => builder.UseCircuitBreaker((CircuitBreakerOptions)null!));
        }
    }

    public sealed class TheSamplingBreaker {
        [Fact]
        public void AddsTheBreakerMiddlewareToThePipeline() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseSamplingCircuitBreaker(), out _);

            Assert.Contains(provider.GetServices<IWebhookMiddleware>(), middleware => middleware is CircuitBreakerMiddleware);
        }

        [Fact]
        public async Task OpensOnlyOnceTheMinimumThroughputIsReachedAndTheFailureRateIsExceeded() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseSamplingCircuitBreaker(options => {
                options.MinimumThroughput = 4;
                options.FailureRateThreshold = 0.5;
                options.SamplingWindow = TimeSpan.FromSeconds(60);
                options.BreakDuration = TimeSpan.FromSeconds(30);
            }), out _);
            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreaker>();

            await FailAsync(breaker, "ep", 3);
            Assert.True((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);

            await breaker.OnFailureAsync("ep", Ct);
            Assert.False((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);
        }

        [Fact]
        public async Task AnEndpointWithMostlySuccessesStaysClosed() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseSamplingCircuitBreaker(options => {
                options.MinimumThroughput = 4;
                options.FailureRateThreshold = 0.5;
            }), out _);
            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreaker>();

            for(int i = 0; i < 6; i++) {
                await breaker.OnSuccessAsync("ep", Ct);
            }
            await FailAsync(breaker, "ep", 2);

            Assert.True((await breaker.TryAcquireAsync("ep", Ct)).IsAllowed);
        }

        [Fact]
        public void InvalidOptionsFailWhenTheyAreRegistered() {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                builder.UseSamplingCircuitBreaker(options => options.FailureRateThreshold = 0));
        }

        [Fact]
        public void ANullBuilderOrNullOptionsIsRefused() {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.ThrowsAny<ArgumentNullException>(() => WebhookBuilderResilienceExtensions.UseSamplingCircuitBreaker(null!));
            Assert.ThrowsAny<ArgumentNullException>(() => builder.UseSamplingCircuitBreaker((Action<SamplingWindowCircuitBreakerOptions>)null!));
            Assert.ThrowsAny<ArgumentNullException>(() => builder.UseSamplingCircuitBreaker((SamplingWindowCircuitBreakerOptions)null!));
        }
    }
}
