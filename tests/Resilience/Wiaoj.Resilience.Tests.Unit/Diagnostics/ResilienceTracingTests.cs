using Microsoft.Extensions.Time.Testing;
using System.Diagnostics;
using Xunit;
using DotNetTimeout = System.Threading.Timeout;

namespace Wiaoj.Resilience.Tests.Unit.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "Tracing")]
public sealed class ResilienceTracingTests {

    private const string SourceName = "Wiaoj.Resilience";
    private const string ExecuteSpanName = "circuit_breaker.execute";
    private const string TimeoutSpanName = "timeout.execute";

    /// <summary>
    /// Collects the activities the library emits for one key. The listener is process-wide and test classes run in
    /// parallel, so filtering on the caller's own key keeps a collector from picking up another test's spans.
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
                    if(activity.GetTagItem("resilience.key") as string != key) {
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

        public Activity Single(string name) => Assert.Single(this.Activities, a => a.OperationName == name);

        public void Dispose() => this._listener.Dispose();
    }

    private static string? Tag(Activity activity, string key) {
        return activity.GetTagItem(key)?.ToString();
    }

    private sealed class StubCircuitBreaker(CircuitExecutionDecision decision) : ICircuitBreaker {
        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(decision);

        public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(decision.State);

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }

    [Collection(ResilienceTracingCollection.Name)]
    public sealed class TheCircuitBreakerExecutionSpan {
        [Fact]
        public async Task ExecuteAsync_WhenOperationSucceeds_EmitsSpanTaggedWithTheClosedOutcome() {
            using SpanCollector collector = new("trace-cb-success");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Allowed());

            int result = await breaker.ExecuteAsync(
                collector.Key,
                _ => ValueTask.FromResult(42),
                TestContext.Current.CancellationToken);

            Assert.Equal(42, result);

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal(collector.Key, Tag(span, "resilience.key"));
            Assert.Equal(nameof(CircuitState.Closed), Tag(span, "resilience.circuit_state"));
            Assert.Equal("success", Tag(span, "resilience.outcome"));
            Assert.Equal(ActivityStatusCode.Ok, span.Status);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCircuitIsOpen_EmitsDeniedSpanCarryingRetryAfter() {
            using SpanCollector collector = new("trace-cb-denied");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(30)));

            await Assert.ThrowsAsync<CircuitBreakerOpenException>(() =>
                breaker.ExecuteAsync(collector.Key, _ => ValueTask.FromResult(1), TestContext.Current.CancellationToken).AsTask());

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal("denied", Tag(span, "resilience.outcome"));
            Assert.Equal(nameof(CircuitState.Open), Tag(span, "resilience.circuit_state"));
            Assert.Equal("30000", Tag(span, "resilience.retry_after_ms"));
            Assert.Equal(ActivityStatusCode.Error, span.Status);
        }

        [Fact]
        public async Task ExecuteAsync_WhenCircuitIsHalfOpen_MarksTheSpanAsAProbe() {
            using SpanCollector collector = new("trace-cb-probe");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.HalfOpenProbe());

            await breaker.ExecuteAsync(collector.Key, _ => ValueTask.FromResult(1), TestContext.Current.CancellationToken);

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal(nameof(CircuitState.HalfOpen), Tag(span, "resilience.circuit_state"));
            Assert.Equal("True", Tag(span, "resilience.probe"));
            Assert.Equal("success", Tag(span, "resilience.outcome"));
        }

        [Fact]
        public async Task ExecuteAsync_WhenOperationThrows_EmitsFailedSpanRecordingTheException() {
            using SpanCollector collector = new("trace-cb-failure");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Allowed());

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<int>(
                    collector.Key,
                    _ => throw new InvalidOperationException("downstream exploded"),
                    TestContext.Current.CancellationToken).AsTask());

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal("failure", Tag(span, "resilience.outcome"));
            Assert.Equal(ActivityStatusCode.Error, span.Status);
            Assert.Contains(span.Events, e => e.Name == "exception");
        }

        [Fact]
        public async Task ExecuteAsync_NonGenericOverload_EmitsTheSameSpan() {
            using SpanCollector collector = new("trace-cb-void");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Allowed());

            await breaker.ExecuteAsync(
                collector.Key,
                _ => ValueTask.CompletedTask,
                TestContext.Current.CancellationToken);

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal("success", Tag(span, "resilience.outcome"));
            Assert.Equal(collector.Key, Tag(span, "resilience.key"));
        }

        [Fact]
        public async Task ExecuteWithFallbackAsync_WhenCircuitIsOpen_StillEmitsTheDeniedSpan() {
            using SpanCollector collector = new("trace-cb-fallback");
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(5)));

            int result = await breaker.ExecuteWithFallbackAsync(
                collector.Key,
                _ => ValueTask.FromResult(1),
                fallbackValue: -1,
                TestContext.Current.CancellationToken);

            Assert.Equal(-1, result);

            Activity span = collector.Single(ExecuteSpanName);
            Assert.Equal("denied", Tag(span, "resilience.outcome"));
        }

        [Fact]
        public async Task ExecuteAsync_WithNoListenerSubscribed_RunsWithoutStartingAnActivity() {
            StubCircuitBreaker breaker = new(CircuitExecutionDecision.Allowed());

            Activity? observedInside = null;
            int result = await breaker.ExecuteAsync(
                "trace-cb-unlistened",
                _ => {
                    observedInside = Activity.Current;
                    return ValueTask.FromResult(7);
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(7, result);
            Assert.Null(observedInside);
        }
    }

    [Collection(ResilienceTracingCollection.Name)]
    public sealed class TheTimeoutExecutionSpan {
        [Fact]
        public async Task ExecuteAsync_WhenOperationCompletesInTime_EmitsSpanWithTheConfiguredDeadline() {
            using SpanCollector collector = new("trace-timeout-success");
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(5), new FakeTimeProvider());

            int result = await strategy.ExecuteAsync(
                collector.Key,
                _ => ValueTask.FromResult(3),
                TestContext.Current.CancellationToken);

            Assert.Equal(3, result);

            Activity span = collector.Single(TimeoutSpanName);
            Assert.Equal(collector.Key, Tag(span, "resilience.key"));
            Assert.Equal("5000", Tag(span, "resilience.timeout_ms"));
            Assert.Equal("success", Tag(span, "resilience.outcome"));
            Assert.Equal(ActivityStatusCode.Ok, span.Status);
        }

        [Fact]
        public async Task ExecuteAsync_WhenDeadlineExpires_EmitsSpanTaggedAsTimedOut() {
            using SpanCollector collector = new("trace-timeout-expired");
            FakeTimeProvider timeProvider = new();
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(2), timeProvider);

            Task execution = strategy.ExecuteAsync(
                collector.Key,
                async token => {
                    await Task.Delay(DotNetTimeout.InfiniteTimeSpan, token);
                    return true;
                },
                TestContext.Current.CancellationToken).AsTask();

            timeProvider.Advance(TimeSpan.FromSeconds(3));

            await Assert.ThrowsAsync<TimeoutException>(() => execution);

            Activity span = collector.Single(TimeoutSpanName);
            Assert.Equal("timeout", Tag(span, "resilience.outcome"));
            Assert.Equal(ActivityStatusCode.Error, span.Status);
        }

        [Fact]
        public async Task ExecuteAsync_NonGenericOverload_EmitsTheSameSpan() {
            using SpanCollector collector = new("trace-timeout-void");
            FixedTimeoutStrategy strategy = new(TimeSpan.FromSeconds(5), new FakeTimeProvider());

            await strategy.ExecuteAsync(
                collector.Key,
                _ => ValueTask.CompletedTask,
                TestContext.Current.CancellationToken);

            Activity span = collector.Single(TimeoutSpanName);
            Assert.Equal("success", Tag(span, "resilience.outcome"));
            Assert.Equal(collector.Key, Tag(span, "resilience.key"));
        }
    }
}
