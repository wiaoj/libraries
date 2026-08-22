using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using Wiaoj.DistributedCounter;
using Wiaoj.Webhooks.DistributedCounter;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.DistributedCounter;

public sealed class DistributedRateLimitingTests {
    private sealed class FakeDistributedCounter : IDistributedCounter {
        private long _current;
        public CounterKey Key { get; init; } = CounterKey.Parse("test");
        public CounterStrategy Strategy => CounterStrategy.Immediate;

        public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
            long val = Interlocked.Add(ref this._current, amount);
            return ValueTask.FromResult(new CounterValue(val));
        }

        public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) {
            long val = Interlocked.Read(ref this._current);
            if(val + amount > limit) {
                return ValueTask.FromResult(new CounterLimitResult(false, val, 0));
            }
            long newVal = Interlocked.Add(ref this._current, amount);
            return ValueTask.FromResult(new CounterLimitResult(true, newVal, limit - newVal));
        }

        public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) {
            long val = Interlocked.Add(ref this._current, -amount);
            return ValueTask.FromResult(new CounterValue(val));
        }

        public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) {
            return ValueTask.FromResult(new CounterLimitResult(true, this._current, 0));
        }

        public ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) {
            return ValueTask.FromResult(new CounterValue(Interlocked.Read(ref this._current)));
        }

        public ValueTask ResetAsync(CancellationToken cancellationToken) {
            Interlocked.Exchange(ref this._current, 0);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeDistributedCounterFactory : IDistributedCounterFactory {
        private readonly ConcurrentDictionary<string, FakeDistributedCounter> _counters = new();

        public IDistributedCounter Create(string name) {
            return this._counters.GetOrAdd(name, _ => new FakeDistributedCounter());
        }

        public IDistributedCounter Create<TTag>() where TTag : notnull {
            return Create(typeof(TTag).Name);
        }

        public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull {
            return Create($"{name}:{key}");
        }

        public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull {
            return Create($"{typeof(TTag).Name}:{key}");
        }
    }

    [Fact]
    public async Task InvokeAsync_AllowsRequestsWithinLimit_AndBlocksExceededRequests() {
        // Arrange
        FakeDistributedCounterFactory factory = new();
        DistributedRateLimitingOptions options = new() {
            MaxRequestsPerWindow = 2,
            Window = TimeSpan.FromSeconds(10)
        };

        DistributedRateLimitingMiddleware middleware = new(
            factory,
            options,
            NullLogger<DistributedRateLimitingMiddleware>.Instance);

        WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
        int downstreamCallCount = 0;
        WebhookDelegate next = (ctx, ct) => {
            downstreamCallCount++;
            return Task.CompletedTask;
        };

        // ── 1st Request (Within limit -> Allowed) ──
        await middleware.InvokeAsync(context, next);
        Assert.Equal(1, downstreamCallCount);

        // ── 2nd Request (Within limit -> Allowed) ──
        await middleware.InvokeAsync(context, next);
        Assert.Equal(2, downstreamCallCount);

        // ── 3rd Request (Limit exceeded -> Blocked without calling downstream) ──
        await middleware.InvokeAsync(context, next);
        Assert.Equal(2, downstreamCallCount); // Downstream must NOT be invoked again

        // Assert: Result must be a TransientFailure containing HTTP 429 and RetryAfter window
        Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
        WebhookDeliveryResult.TransientFailure transient = Assert.IsType<WebhookDeliveryResult.TransientFailure>(result);
        Assert.False(transient.IsSuccess);
        Assert.Equal(429, transient.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(10), transient.RetryAfter);
        Assert.Contains("Rate limit", transient.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Options_Validate_Throws_OnInvalidValues() {
        DistributedRateLimitingOptions options = new() {
            MaxRequestsPerWindow = 0
        };
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.MaxRequestsPerWindow = 10;
        options.Window = TimeSpan.Zero;
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.Window = TimeSpan.FromSeconds(-1);
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());

        options.Window = TimeSpan.FromSeconds(1);
        options.KeySelector = null!;
        Assert.ThrowsAny<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void UseDistributedRateLimiting_RegistersMiddlewareInContainer() {
        ServiceCollection services = new();
        FakeDistributedCounterFactory factory = new();

        services.AddLogging();
        services.AddSingleton<IDistributedCounterFactory>(factory);
        services.AddSingleton<IWebhookTransport, FakeWebhookTransport>();

        services.AddWiaojWebhooks(options => {
            options.UseDistributedRateLimiting(100, TimeSpan.FromSeconds(1));
        });

        ServiceProvider sp = services.BuildServiceProvider();
        DistributedRateLimitingMiddleware middleware = sp.GetRequiredService<DistributedRateLimitingMiddleware>();
        Assert.NotNull(middleware);
    }
}