using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Diagnostics;
using Wiaoj.DistributedCounter;
using Wiaoj.Resilience.Tests.Unit.Diagnostics;

namespace Wiaoj.Resilience.Tests.Unit.CircuitBreaker;

/// <summary>
/// A circuit whose store is unreachable (#90): an operation that ran is never reported as failed or run twice, and a
/// resilient breaker steps aside instead of refusing every call.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "Resilience")]
[Trait("Component", "StorageFailure")]
public sealed class StorageFailureTests {

    /// <summary>What a Redis client throws when it cannot reach the server.</summary>
    public sealed class StoreUnavailableException() : Exception("The counter store is unreachable.");

    /// <summary>A counter store that is down: every operation throws.</summary>
    private sealed class UnreachableCounterFactory(Func<Exception> failure) : IDistributedCounterFactory {
        public IDistributedCounter Create(string name) => new UnreachableCounter(failure);
        public IDistributedCounter Create<TTag>() where TTag : notnull => new UnreachableCounter(failure);
        public IDistributedCounter Create<TKey>(string name, TKey key) where TKey : notnull => new UnreachableCounter(failure);
        public IDistributedCounter Create<TTag, TKey>(TKey key) where TTag : notnull where TKey : notnull => new UnreachableCounter(failure);
    }

    private sealed class UnreachableCounter(Func<Exception> failure) : IDistributedCounter {
        public CounterKey Key => new("unreachable");
        public CounterStrategy Strategy => default;
        public ValueTask<CounterValue> IncrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) => throw failure();
        public ValueTask<CounterLimitResult> TryIncrementAsync(long amount, long limit, CounterExpiry expiry, CancellationToken cancellationToken) => throw failure();
        public ValueTask<CounterValue> DecrementAsync(long amount, CounterExpiry expiry, CancellationToken cancellationToken) => throw failure();
        public ValueTask<CounterLimitResult> TryDecrementAsync(long amount, long minLimit, CounterExpiry expiry, CancellationToken cancellationToken) => throw failure();
        public ValueTask<bool> TryCompareExchangeAsync(CounterValue expectedValue, CounterValue newValue, CounterExpiry expiry, CancellationToken cancellationToken) => throw failure();
        public ValueTask<CounterValue> GetValueAsync(CancellationToken cancellationToken) => throw failure();
        public ValueTask ResetAsync(CancellationToken cancellationToken) => throw failure();
        public ValueTask SetAsync(long value, CounterExpiry expiry = default, CancellationToken cancellationToken = default) => throw failure();
    }

    /// <summary>
    /// A breaker whose store answered the acquire, then went away before the outcome was recorded — the window the
    /// original defect lived in.
    /// </summary>
    private sealed class FailsAfterAcquireBreaker : ICircuitBreaker {
        public int Successes;
        public int Failures;

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) => ValueTask.FromResult(CircuitExecutionDecision.Allowed());
        public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) => ValueTask.FromResult(CircuitState.Closed);

        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this.Successes);
            throw new StoreUnavailableException();
        }

        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) {
            Interlocked.Increment(ref this.Failures);
            throw new StoreUnavailableException();
        }
    }

    private sealed class ScriptedBreaker(Func<Exception>? throws = null) : ICircuitBreaker {
        public CircuitExecutionDecision Decision { get; init; } = CircuitExecutionDecision.Allowed();
        public CircuitState State { get; init; } = CircuitState.Closed;

        public ValueTask<CircuitExecutionDecision> TryAcquireAsync(string key, CancellationToken cancellationToken = default) => throws is null ? ValueTask.FromResult(this.Decision) : throw throws();
        public ValueTask<CircuitState> GetStateAsync(string key, CancellationToken cancellationToken = default) => throws is null ? ValueTask.FromResult(this.State) : throw throws();
        public ValueTask OnSuccessAsync(string key, CancellationToken cancellationToken = default) => throws is null ? ValueTask.CompletedTask : throw throws();
        public ValueTask OnFailureAsync(string key, CancellationToken cancellationToken = default) => throws is null ? ValueTask.CompletedTask : throw throws();
    }

    private sealed class RecordingLogger<T> : ILogger<T> {
        private readonly Lock _gate = new();
        private readonly List<(EventId Id, Exception? Exception)> _entries = [];

        public IReadOnlyList<(EventId Id, Exception? Exception)> Entries {
            get { lock(this._gate) { return [.. this._entries]; } }
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            lock(this._gate) { this._entries.Add((eventId, exception)); }
        }
    }

    private static ConsecutiveFailuresCircuitBreaker BreakerOverUnreachableStore(Func<Exception>? failure = null) {
        return new ConsecutiveFailuresCircuitBreaker(
            new UnreachableCounterFactory(failure ?? (() => new StoreUnavailableException())),
            new CircuitBreakerOptions { FailureThreshold = 2, BreakDuration = TimeSpan.FromSeconds(30) },
            new FakeTimeProvider(),
            NullLogger<ConsecutiveFailuresCircuitBreaker>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// The execution extensions, against a store that fails after the acquire. No decorator: the fix is in the extensions.
    /// </summary>
    public sealed class Execution {
        [Fact]
        public async Task A_Successful_Operation_Should_Run_Once_And_Return_Its_Result() {
            // Before: OnSuccessAsync threw, the catch recorded a failure, and the caller got an exception for
            // completed work — a retry then sent the same notification twice.
            FailsAfterAcquireBreaker breaker = new();
            int runs = 0;

            string result = await breaker.ExecuteAsync("provider", _ => {
                runs++;
                return ValueTask.FromResult("accepted");
            }, Ct);

            Assert.Equal("accepted", result);
            Assert.Equal(1, runs);
            Assert.Equal((1, 0), (breaker.Successes, breaker.Failures));
        }

        [Fact]
        public async Task A_Successful_Non_Returning_Operation_Should_Run_Once_And_Complete() {
            FailsAfterAcquireBreaker breaker = new();
            int runs = 0;

            await breaker.ExecuteAsync("provider", _ => {
                runs++;
                return ValueTask.CompletedTask;
            }, Ct);

            Assert.Equal(1, runs);
            Assert.Equal((1, 0), (breaker.Successes, breaker.Failures));
        }

        [Fact]
        public async Task A_Failing_Operation_Should_Surface_Its_Own_Exception_Not_The_Stores() {
            FailsAfterAcquireBreaker breaker = new();
            InvalidOperationException own = new("provider rejected the request");

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<string>("provider", _ => throw own, Ct).AsTask());

            Assert.Same(own, error);
            Assert.Equal((0, 1), (breaker.Successes, breaker.Failures));
        }

        [Fact]
        public async Task A_Failing_Non_Returning_Operation_Should_Surface_Its_Own_Exception() {
            FailsAfterAcquireBreaker breaker = new();
            TimeoutException own = new("provider timed out");

            TimeoutException error = await Assert.ThrowsAsync<TimeoutException>(() =>
                breaker.ExecuteAsync("provider", _ => throw own, Ct).AsTask());

            Assert.Same(own, error);
        }

        [Fact]
        public async Task A_Fallback_Value_Should_Not_Replace_The_Result_Of_A_Successful_Operation() {
            FailsAfterAcquireBreaker breaker = new();
            int runs = 0;

            string result = await breaker.ExecuteWithFallbackAsync("provider", _ => {
                runs++;
                return ValueTask.FromResult("accepted");
            }, "fallback", Ct);

            Assert.Equal(("accepted", 1), (result, runs));
        }

        [Fact]
        public async Task A_Fallback_Factory_Should_Receive_The_Operations_Own_Exception() {
            FailsAfterAcquireBreaker breaker = new();
            InvalidOperationException own = new("rejected");
            Exception? received = null;

            string result = await breaker.ExecuteWithFallbackAsync<string>("provider", _ => throw own, (exception, _) => {
                received = exception;
                return ValueTask.FromResult("fallback");
            }, Ct);

            Assert.Equal("fallback", result);
            Assert.Same(own, received);
        }

        [Fact]
        public async Task A_Fallback_Factory_Should_Not_Run_For_A_Successful_Operation() {
            FailsAfterAcquireBreaker breaker = new();
            bool fallbackRan = false;

            string result = await breaker.ExecuteWithFallbackAsync("provider", _ => ValueTask.FromResult("accepted"), (_, _) => {
                fallbackRan = true;
                return ValueTask.FromResult("fallback");
            }, Ct);

            Assert.Equal("accepted", result);
            Assert.False(fallbackRan);
        }

        [Fact]
        public async Task Cancellation_Should_Still_Propagate_Without_Recording_A_Failure() {
            FailsAfterAcquireBreaker breaker = new();
            using CancellationTokenSource cts = new();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breaker.ExecuteAsync<string>("provider", token => {
                cts.Cancel();
                token.ThrowIfCancellationRequested();
                return ValueTask.FromResult("unreachable");
            }, cts.Token).AsTask());

            Assert.Equal((0, 0), (breaker.Successes, breaker.Failures));
        }

        [Fact]
        public async Task Without_A_Resilient_Breaker_An_Unreachable_Store_Should_Refuse_Before_The_Operation_Runs() {
            // Acquiring is still strict: nothing has run, so nothing can be repeated. Fail-open is opt-in.
            ConsecutiveFailuresCircuitBreaker breaker = BreakerOverUnreachableStore();
            int runs = 0;

            await Assert.ThrowsAsync<StoreUnavailableException>(() => breaker.ExecuteAsync("provider", _ => {
                runs++;
                return ValueTask.FromResult(1);
            }, Ct).AsTask());

            Assert.Equal(0, runs);
        }
    }

    /// <summary>The unrecorded outcome is still visible: the span keeps the operation's outcome and carries an event.</summary>
    [Collection(ResilienceTracingCollection.Name)]
    public sealed class Tracing {
        [Fact]
        public async Task A_Failed_Recording_Should_Be_An_Event_On_A_Span_That_Still_Reports_Success() {
            string key = $"provider-{Guid.NewGuid():N}";
            List<Activity> spans = [];
            using ActivityListener listener = new() {
                ShouldListenTo = source => source.Name == "Wiaoj.Resilience",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => {
                    if(activity.GetTagItem("resilience.key") as string == key) {
                        lock(spans) { spans.Add(activity); }
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);

            await new FailsAfterAcquireBreaker().ExecuteAsync(key, _ => ValueTask.FromResult(1), Ct);

            Activity span = Assert.Single(spans);
            Assert.Equal("success", span.GetTagItem("resilience.outcome"));
            Assert.Equal(ActivityStatusCode.Ok, span.Status);
            ActivityEvent recorded = Assert.Single(span.Events, e => e.Name == "circuit_breaker.record_failed");
            Assert.Contains(recorded.Tags, t => t.Key == "exception.type" && (string?)t.Value == typeof(StoreUnavailableException).FullName);
        }
    }

    /// <summary>The decorator over a real breaker whose store throws on every call.</summary>
    public sealed class AResilientBreaker {
        [Fact]
        public async Task Should_Allow_The_Call_When_Acquiring_Fails_And_Log_It() {
            RecordingLogger<ResilientCircuitBreaker> logger = new();
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore(), new ResilientCircuitBreakerOptions(), logger);

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync("provider", Ct);

            Assert.True(decision.IsAllowed);
            Assert.Equal(CircuitState.Closed, decision.State);
            Assert.Contains(logger.Entries, e => e.Id.Id == 2006 && e.Exception is StoreUnavailableException);
        }

        [Fact]
        public async Task Should_Report_Closed_When_The_State_Cannot_Be_Read_By_Default() {
            RecordingLogger<ResilientCircuitBreaker> logger = new();
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore(), new ResilientCircuitBreakerOptions(), logger);

            Assert.Equal(CircuitState.Closed, await breaker.GetStateAsync("provider", Ct));
            Assert.Contains(logger.Entries, e => e.Id.Id == 2007);
        }

        [Fact]
        public async Task Should_Rethrow_When_The_State_Cannot_Be_Read_And_Unknown_Must_Not_Look_Healthy() {
            ResilientCircuitBreaker breaker = new(
                BreakerOverUnreachableStore(),
                new ResilientCircuitBreakerOptions { StateOnStorageFailure = StorageFailureState.Throw },
                NullLogger<ResilientCircuitBreaker>.Instance);

            await Assert.ThrowsAsync<StoreUnavailableException>(() => breaker.GetStateAsync("provider", Ct).AsTask());
        }

        [Fact]
        public async Task Should_Swallow_And_Log_A_Failure_To_Record_Either_Outcome() {
            RecordingLogger<ResilientCircuitBreaker> logger = new();
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore(), new ResilientCircuitBreakerOptions(), logger);

            await breaker.OnSuccessAsync("provider", Ct);
            await breaker.OnFailureAsync("provider", Ct);

            Assert.Equal(2, logger.Entries.Count(e => e.Id.Id == 2008));
        }

        [Fact]
        public async Task Should_Run_A_Protected_Operation_Exactly_Once_And_Return_Its_Result() {
            // The acceptance criterion end to end: the real breaker, the store down on every call.
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore());
            int runs = 0;

            string result = await breaker.ExecuteAsync("provider", _ => {
                runs++;
                return ValueTask.FromResult("accepted");
            }, Ct);

            Assert.Equal(("accepted", 1), (result, runs));
        }

        [Fact]
        public async Task Should_Surface_A_Failing_Operations_Own_Exception() {
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore());
            InvalidOperationException own = new("rejected");

            InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                breaker.ExecuteAsync<string>("provider", _ => throw own, Ct).AsTask());

            Assert.Same(own, error);
        }

        [Fact]
        public async Task Should_Treat_A_Store_Timeout_It_Did_Not_Request_As_A_Store_Failure() {
            // A client's own timeout surfaces as TaskCanceledException without the caller cancelling anything.
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore(() => new TaskCanceledException("store timed out")));

            Assert.True((await breaker.TryAcquireAsync("provider", Ct)).IsAllowed);
        }

        [Fact]
        public async Task Should_Propagate_Cancellation_The_Caller_Requested() {
            using CancellationTokenSource cts = new();
            cts.Cancel();
            ResilientCircuitBreaker breaker = new(BreakerOverUnreachableStore(() => new OperationCanceledException(cts.Token)));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breaker.TryAcquireAsync("provider", cts.Token).AsTask());
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => breaker.OnFailureAsync("provider", cts.Token).AsTask());
        }

        [Fact]
        public async Task Should_Not_Hide_A_Callers_Invalid_Argument() {
            ResilientCircuitBreaker breaker = new(new ScriptedBreaker(() => new ArgumentException("key")));

            await Assert.ThrowsAsync<ArgumentException>(() => breaker.TryAcquireAsync("provider", Ct).AsTask());
        }

        [Fact]
        public async Task Should_Pass_A_Healthy_Breakers_Answers_Through_Unchanged() {
            ResilientCircuitBreaker breaker = new(new ScriptedBreaker {
                Decision = CircuitExecutionDecision.Denied(TimeSpan.FromSeconds(12)),
                State = CircuitState.Open
            });

            CircuitExecutionDecision decision = await breaker.TryAcquireAsync("provider", Ct);

            Assert.False(decision.IsAllowed);
            Assert.Equal(TimeSpan.FromSeconds(12), decision.RetryAfter);
            Assert.Equal(CircuitState.Open, await breaker.GetStateAsync("provider", Ct));
        }
    }

    /// <summary><c>FailOpenOnStorageFailure()</c> on the builder, so every consumer of the factory gets it.</summary>
    public sealed class TheBuilder {
        private sealed class ProviderPolicy;

        private static ServiceProvider Build(Action<IResilienceBuilder> configure) {
            ServiceCollection services = new();
            services.AddSingleton<IDistributedCounterFactory>(new UnreachableCounterFactory(() => new StoreUnavailableException()));
            services.AddSingleton<TimeProvider>(new FakeTimeProvider());
            services.AddWiaojResilience(configure);
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task Should_Wrap_Named_Default_And_Typed_Policies() {
            using ServiceProvider provider = Build(r => r
                .AddConsecutiveBreaker("named", o => o.FailureThreshold = 2)
                .AddConsecutiveBreaker<ProviderPolicy>(o => o.FailureThreshold = 2)
                .UseDefaultConsecutiveBreaker(o => o.FailureThreshold = 2)
                .FailOpenOnStorageFailure());

            ICircuitBreakerFactory factory = provider.GetRequiredService<ICircuitBreakerFactory>();

            Assert.IsType<ResilientCircuitBreaker>(factory.Create("named"));
            Assert.IsType<ResilientCircuitBreaker>(factory.Create());
            Assert.True((await provider.GetRequiredService<ICircuitBreaker<ProviderPolicy>>().TryAcquireAsync("provider", Ct)).IsAllowed);
        }

        [Fact]
        public async Task Should_Let_A_Composite_Through_When_Its_Store_Is_Down() {
            using ServiceProvider provider = Build(r => r
                .AddConsecutiveBreaker("fast", o => o.FailureThreshold = 2)
                .AddSamplingBreaker("slow", o => { })
                .AddCompositeBreaker("both", "fast", "slow")
                .FailOpenOnStorageFailure());

            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreakerFactory>().Create("both");
            int runs = 0;

            int result = await breaker.ExecuteAsync("provider", _ => ValueTask.FromResult(++runs), Ct);

            Assert.Equal((1, 1), (result, runs));
        }

        [Fact]
        public async Task Should_Apply_The_Configured_State_Behaviour() {
            using ServiceProvider provider = Build(r => r
                .AddConsecutiveBreaker("named", o => o.FailureThreshold = 2)
                .FailOpenOnStorageFailure(o => o.StateOnStorageFailure = StorageFailureState.Throw));

            ICircuitBreaker breaker = provider.GetRequiredService<ICircuitBreakerFactory>().Create("named");

            await Assert.ThrowsAsync<StoreUnavailableException>(() => breaker.GetStateAsync("provider", Ct).AsTask());
        }

        [Fact]
        public void Should_Hand_Out_Breakers_Unwrapped_Unless_Asked() {
            using ServiceProvider provider = Build(r => r.AddConsecutiveBreaker("named", o => o.FailureThreshold = 2));

            Assert.IsType<ConsecutiveFailuresCircuitBreaker>(provider.GetRequiredService<ICircuitBreakerFactory>().Create("named"));
        }
    }
}
