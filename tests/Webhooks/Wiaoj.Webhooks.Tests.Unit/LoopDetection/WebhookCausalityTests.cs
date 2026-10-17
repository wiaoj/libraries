using System.Reflection;
using Wiaoj.Webhooks.Diagnostics;
using Wiaoj.Webhooks.LoopDetection;

namespace Wiaoj.Webhooks.Tests.Unit.LoopDetection;

[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "Causality")]
public sealed class WebhookCausalityTests {

    public sealed class TheCausalityValue {

        [Fact]
        public void None_HasNoHopsAndNoChain() {
            WebhookCausality none = WebhookCausality.None;

            Assert.Equal(0, none.HopCount);
            Assert.Null(none.CausalChain);
            Assert.True(none.IsNone);
        }

        [Fact]
        public void Constructor_WhenHopCountIsNegative_Throws() {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new WebhookCausality(-1, "svc-a"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Constructor_WhenChainIsBlank_StoresNull(string? chain) {
            WebhookCausality causality = new(2, chain);

            Assert.Null(causality.CausalChain);
            Assert.False(causality.IsNone);
        }

        [Fact]
        public void Constructor_TrimsTheChain() {
            WebhookCausality causality = new(1, "  svc-a, svc-b  ");

            Assert.Equal("svc-a, svc-b", causality.CausalChain);
        }

        [Theory]
        [InlineData("svc-a, svc-b", "svc-b", true)]
        [InlineData("svc-a, svc-b", "SVC-A", true)]
        [InlineData("\"svc-a\" , svc-b", "svc-a", true)]
        [InlineData("svc-10, svc-100", "svc-1", false)]
        [InlineData("svc-a", "svc-b", false)]
        public void Contains_MatchesWholeTokensCaseInsensitively(string chain, string serviceName, bool expected) {
            Assert.Equal(expected, new WebhookCausality(1, chain).Contains(serviceName));
        }

        [Fact]
        public void Contains_WhenChainIsEmpty_ReturnsFalse() {
            Assert.False(WebhookCausality.None.Contains("svc-a"));
            Assert.False(WebhookCausality.ChainContains(null, "svc-a"));
        }
    }

    public sealed class TheAmbientScope {

        [Fact]
        public void Current_OutsideAnyScope_IsNone() {
            Assert.True(WebhookCausalityScope.Current.IsNone);
        }

        [Fact]
        public void Begin_SetsCurrent_AndDisposeRestoresThePreviousValue() {
            WebhookCausality outer = new(1, "svc-a");
            WebhookCausality inner = new(2, "svc-a, svc-b");

            using(WebhookCausalityScope.Begin(outer)) {
                Assert.Equal(outer, WebhookCausalityScope.Current);

                using(WebhookCausalityScope.Begin(inner)) {
                    Assert.Equal(inner, WebhookCausalityScope.Current);
                }

                Assert.Equal(outer, WebhookCausalityScope.Current);
            }

            Assert.True(WebhookCausalityScope.Current.IsNone);
        }

        [Fact]
        public void Dispose_Twice_DoesNotRestoreAgain() {
            WebhookCausality outer = new(1, "svc-a");

            using(WebhookCausalityScope.Begin(outer)) {
                IDisposable inner = WebhookCausalityScope.Begin(new WebhookCausality(2, "svc-b"));
                inner.Dispose();

                using(WebhookCausalityScope.Begin(new WebhookCausality(3, "svc-c"))) {
                    // A second dispose of the finished scope must not clobber the scope opened after it.
                    inner.Dispose();
                    Assert.Equal(3, WebhookCausalityScope.Current.HopCount);
                }

                Assert.Equal(outer, WebhookCausalityScope.Current);
            }
        }

        [Fact]
        public async Task Current_FlowsAcrossAwaitsAndIntoSpawnedTasks() {
            WebhookCausality causality = new(2, "svc-a, svc-b");

            using(WebhookCausalityScope.Begin(causality)) {
                await Task.Yield();
                Assert.Equal(causality, WebhookCausalityScope.Current);

                WebhookCausality seenByTask = await Task.Run(() => WebhookCausalityScope.Current, TestContext.Current.CancellationToken);
                Assert.Equal(causality, seenByTask);
            }
        }

        [Fact]
        public async Task Current_IsIsolatedBetweenConcurrentFlows() {
            TaskCompletionSource bothInside = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int inside = 0;

            async Task<int> RunAsync(int hops) {
                using(WebhookCausalityScope.Begin(new WebhookCausality(hops, $"svc-{hops}"))) {
                    if(Interlocked.Increment(ref inside) == 2) {
                        bothInside.SetResult();
                    }

                    await bothInside.Task.WaitAsync(TestContext.Current.CancellationToken);
                    return WebhookCausalityScope.Current.HopCount;
                }
            }

            int[] seen = await Task.WhenAll(Task.Run(() => RunAsync(1)), Task.Run(() => RunAsync(7)));

            Assert.Equal([1, 7], seen);
        }
    }

    public sealed class TheServiceName {

        [Fact]
        public void Resolve_PrefersOpenTelemetryServiceName() {
            Assert.Equal("billing", WebhookServiceName.Resolve("  billing ", typeof(WebhookCausalityTests).Assembly));
        }

        [Fact]
        public void Resolve_ReplacesCommasInTheOpenTelemetryServiceName() {
            Assert.Equal("billing_eu", WebhookServiceName.Resolve("billing,eu", null));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Resolve_FallsBackToTheEntryAssemblyName(string? openTelemetryServiceName) {
            Assembly assembly = typeof(WebhookCausalityTests).Assembly;

            Assert.Equal(assembly.GetName().Name, WebhookServiceName.Resolve(openTelemetryServiceName, assembly));
        }

        [Fact]
        public void Resolve_WithoutAnySource_ReturnsUnknown() {
            Assert.Equal(WebhookServiceName.Unknown, WebhookServiceName.Resolve(null, null));
        }

        [Fact]
        public void Resolve_IsNotThePodInstanceId() {
            // The instance id is unique per process; the service name must be the same for every pod of a service.
            Assert.Equal(WebhookServiceName.Resolve(), WebhookServiceName.Resolve());
            Assert.DoesNotContain(Environment.ProcessId.ToString(), WebhookServiceName.Resolve(null, typeof(WebhookCausalityTests).Assembly));
        }
    }

    public sealed class TheOptions {

        [Fact]
        public void Defaults_AreEnabled_WithTheResolvedServiceName() {
            LoopDetectionOptions options = new();

            Assert.True(options.Enabled);
            Assert.Equal(WebhookServiceName.Resolve(), options.ServiceName);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("svc-a,svc-b")]
        public void ServiceName_WhenBlankOrContainsAComma_Throws(string? invalid) {
            LoopDetectionOptions options = new();

            Assert.ThrowsAny<ArgumentException>(() => options.ServiceName = invalid!);
        }

        [Fact]
        public void ServiceName_IsTrimmed() {
            LoopDetectionOptions options = new() { ServiceName = "  billing  " };

            Assert.Equal("billing", options.ServiceName);
        }
    }
}
