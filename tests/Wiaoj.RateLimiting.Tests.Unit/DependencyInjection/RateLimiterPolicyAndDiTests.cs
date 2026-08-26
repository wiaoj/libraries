using Microsoft.Extensions.DependencyInjection;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting.DependencyInjection;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.DependencyInjection;

[Trait("Category", "Unit")]
[Trait("Component", "DependencyInjection")]
[Trait("Feature", "PolicyResolution")]
public sealed class RateLimiterPolicyAndDiTests {

    public sealed class TheServiceRegistrationAndResolution {

        [Fact]
        public void AddWiaojRateLimiting_RegistersCoreServicesWithExpectedLifetimes() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            // Act
            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy("auth", policy => policy.UseFixedWindow(5, TimeSpan.FromMinutes(1)));
                limiter.AddPolicy<OrderPolicy>(policy => policy.UseSlidingWindow(10, TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();

            // Assert: IRateLimiter is Singleton
            IRateLimiter limiter1 = provider.GetRequiredService<IRateLimiter>();
            IRateLimiter limiter2 = provider.GetRequiredService<IRateLimiter>();
            Assert.Same(limiter1, limiter2);

            // Assert: IRateLimiter<TPolicy> is Transient wrapper
            IRateLimiter<OrderPolicy> typed1 = provider.GetRequiredService<IRateLimiter<OrderPolicy>>();
            IRateLimiter<OrderPolicy> typed2 = provider.GetRequiredService<IRateLimiter<OrderPolicy>>();
            Assert.NotSame(typed1, typed2);
        }

        [Fact]
        public async Task NamedPolicy_ResolvesConfiguredAlgorithmAndEnforcesLimits() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy("login", policy => policy.UseFixedWindow(limit: 2, window: TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act & Assert
            RateLimitDecision d1 = await limiter.TryAcquireAsync("login", "client_1", ct);
            RateLimitDecision d2 = await limiter.TryAcquireAsync("login", "client_1", ct);
            RateLimitDecision d3 = await limiter.TryAcquireAsync("login", "client_1", ct); // Over limit!

            Assert.True(d1.IsAllowed);
            Assert.True(d2.IsAllowed);
            Assert.False(d3.IsAllowed);
        }

        [Fact]
        public async Task TypedPolicyWrapper_DelegatesToMatchingPolicyName() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy<PaymentPolicy>(policy => policy.UseFixedWindow(limit: 1, window: TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter<PaymentPolicy> typedLimiter = provider.GetRequiredService<IRateLimiter<PaymentPolicy>>();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act & Assert
            RateLimitDecision d1 = await typedLimiter.TryAcquireAsync("user_100", ct);
            RateLimitDecision d2 = await typedLimiter.TryAcquireAsync("user_100", ct);

            Assert.True(d1.IsAllowed);
            Assert.False(d2.IsAllowed);
        }
    }

    public sealed class TheDefaultPolicyExecution {

        [Fact]
        public async Task DefaultPolicy_WhenConfigured_ResolvesForUntaggedRequests() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            services.AddWiaojRateLimiting(limiter => {
                limiter.UseDefaultPolicy(policy => policy.UseFixedWindow(limit: 3, window: TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act: Request without specifying policy name
            RateLimitDecision d1 = await limiter.TryAcquireAsync("anon_key", ct);
            RateLimitDecision d2 = await limiter.TryAcquireAsync("anon_key", ct);
            RateLimitDecision d3 = await limiter.TryAcquireAsync("anon_key", ct);
            RateLimitDecision d4 = await limiter.TryAcquireAsync("anon_key", ct);

            // Assert
            Assert.True(d1.IsAllowed);
            Assert.True(d2.IsAllowed);
            Assert.True(d3.IsAllowed);
            Assert.False(d4.IsAllowed);
        }

        [Fact]
        public async Task DefaultPolicy_WhenNotConfigured_ThrowsInvalidOperationException() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy("explicit_only", policy => policy.UseFixedWindow(5, TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() =>
                limiter.TryAcquireAsync("any_key", TestContext.Current.CancellationToken).AsTask());
        }
    }

    public sealed class TheDecoratorIntegration {

        [Fact]
        public async Task PolicyWithDecorators_AppliesResilienceAndNegativeCachingInPipeline() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());

            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy("protected_endpoint", policy => {
                    policy.UseFixedWindow(limit: 1, window: TimeSpan.FromSeconds(30))
                          .WithNegativeCaching()
                          .WithFailOpen();
                });
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();
            CancellationToken ct = TestContext.Current.CancellationToken;

            // Act 1: Consume limit
            RateLimitDecision d1 = await limiter.TryAcquireAsync("protected_endpoint", "spammer", ct);
            Assert.True(d1.IsAllowed);

            // Act 2: Denied and cached in L1 negative cache
            RateLimitDecision d2 = await limiter.TryAcquireAsync("protected_endpoint", "spammer", ct);
            Assert.False(d2.IsAllowed);
            Assert.NotNull(d2.RetryAfter);
        }
    }

    public sealed class TheErrorHandlingAndEdgeCases {

        [Fact]
        public async Task NonExistentPolicyName_ThrowsKeyNotFoundException() {
            // Arrange
            ServiceCollection services = new();
            services.AddDistributedCounter(dc => dc.UseInMemory());
            services.AddWiaojRateLimiting(limiter => {
                limiter.AddPolicy("valid_policy", policy => policy.UseFixedWindow(5, TimeSpan.FromMinutes(1)));
            });

            using ServiceProvider provider = services.BuildServiceProvider();
            IRateLimiter limiter = provider.GetRequiredService<IRateLimiter>();

            // Act & Assert
            await Assert.ThrowsAnyAsync<KeyNotFoundException>(() =>
                limiter.TryAcquireAsync("missing_policy", "some_key", TestContext.Current.CancellationToken).AsTask());
        }
    }

    private sealed class OrderPolicy;
    private sealed class PaymentPolicy;
}