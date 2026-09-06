using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Wiaoj.DistributedCounter;
using Wiaoj.RateLimiting.DependencyInjection;
using Xunit;

namespace Wiaoj.RateLimiting.Tests.Unit.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Feature", "RateLimiting")]
[Trait("Component", "Tracing")]
[Collection(RateLimitingTracingCollection.Name)]
public sealed class RateLimitingTracingTests {

    private const string SourceName = "Wiaoj.RateLimiting";
    private const string AcquireSpanName = "rate_limit.acquire";

    /// <summary>
    /// Collects the activities emitted for one key. The listener is process-wide and test classes run in parallel,
    /// so filtering on the caller's own key keeps a collector from picking up another test's spans.
    /// </summary>
    private sealed class SpanCollector : IDisposable {
        private readonly ActivityListener _listener;
        private readonly List<Activity> _activities = [];
        private readonly Lock _gate = new();

        public SpanCollector(string key) {
            this.Key = key;
            this._listener = new ActivityListener {
                ShouldListenTo = source => source.Name == SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => {
                    if(activity.GetTagItem("rate_limit.key") as string != key) {
                        return;
                    }

                    lock(this._gate) {
                        this._activities.Add(activity);
                    }
                }
            };

            ActivitySource.AddActivityListener(this._listener);
        }

        public string Key { get; }

        public IReadOnlyList<Activity> Activities {
            get {
                lock(this._gate) {
                    return [.. this._activities];
                }
            }
        }

        public Activity Single() => Assert.Single(this.Activities, a => a.OperationName == AcquireSpanName);

        public void Dispose() => this._listener.Dispose();
    }

    private static string? Tag(Activity activity, string key) => activity.GetTagItem(key)?.ToString();

    private static IRateLimiter CreateLimiter(int limit) {
        ServiceCollection services = new();
        services.AddDistributedCounter(c => c.UseInMemory());
        services.AddWiaojRateLimiting(limiter => {
            limiter.AddPolicy("test-policy", policy =>
                policy.UseFixedWindow(limit: limit, window: TimeSpan.FromMinutes(5)));

            limiter.UseDefaultPolicy(policy =>
                policy.UseFixedWindow(limit: limit, window: TimeSpan.FromMinutes(5)));
        });

        return services.BuildServiceProvider().GetRequiredService<IRateLimiter>();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenAllowed_EmitsSpanTaggedWithThePolicyAndDecision() {
        using SpanCollector collector = new("trace-rl-allowed");
        IRateLimiter limiter = CreateLimiter(limit: 5);

        RateLimitDecision decision = await limiter.TryAcquireAsync(
            "test-policy", collector.Key, 1, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);

        Activity span = collector.Single();
        Assert.Equal("test-policy", Tag(span, "rate_limit.policy"));
        Assert.Equal(collector.Key, Tag(span, "rate_limit.key"));
        Assert.Equal("1", Tag(span, "rate_limit.cost"));
        Assert.Equal("allowed", Tag(span, "rate_limit.decision"));
    }

    [Fact]
    public async Task TryAcquireAsync_WhenDenied_TagsTheDecisionWithoutFlaggingTheSpanAsAnError() {
        using SpanCollector collector = new("trace-rl-denied");
        IRateLimiter limiter = CreateLimiter(limit: 1);

        await limiter.TryAcquireAsync("test-policy", collector.Key, 1, TestContext.Current.CancellationToken);
        RateLimitDecision denied = await limiter.TryAcquireAsync(
            "test-policy", collector.Key, 1, TestContext.Current.CancellationToken);

        Assert.False(denied.IsAllowed);

        Activity span = Assert.Single(collector.Activities, a => Tag(a, "rate_limit.decision") == "denied");

        // Exceeding a quota is the limiter working as designed, not a failed operation.
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
    }

    [Fact]
    public async Task TryAcquireAsync_OnTheDefaultPolicyOverload_TagsThePolicyAsDefault() {
        using SpanCollector collector = new("trace-rl-default");
        IRateLimiter limiter = CreateLimiter(limit: 5);

        await limiter.TryAcquireAsync(collector.Key, 1, TestContext.Current.CancellationToken);

        Activity span = collector.Single();
        Assert.Equal("Default", Tag(span, "rate_limit.policy"));
    }

    [Fact]
    public async Task TryAcquireAsync_WithNoListenerSubscribed_StartsNoActivity() {
        IRateLimiter limiter = CreateLimiter(limit: 5);

        RateLimitDecision decision = await limiter.TryAcquireAsync(
            "test-policy", "trace-rl-unlistened", 1, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Null(Activity.Current);
    }
}
