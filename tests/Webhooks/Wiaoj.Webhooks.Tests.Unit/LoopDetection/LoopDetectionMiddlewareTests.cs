using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.LoopDetection;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.LoopDetection;

[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "Middleware")]
public sealed class LoopDetectionMiddlewareTests {

    public sealed class TheHopLimitEvaluation {

        [Fact]
        public async Task InvokeAsync_WhenHeaderMissing_SetsInitialHopCount_AndCallsNext() {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 5, InstanceId = "node-1" };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("1", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
            Assert.Equal("node-1", context.GetHeader(WebhookHeaderNames.WebhookCausalChain));
        }

        [Fact]
        public async Task InvokeAsync_WhenHopCountBelowMax_IncrementsHopCount_AndCallsNext() {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 5, InstanceId = "node-2" };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "2");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, "node-1");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("3", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
            Assert.Equal("node-1, node-2", context.GetHeader(WebhookHeaderNames.WebhookCausalChain));
        }

        [Theory]
        [InlineData(3, 3)]
        [InlineData(3, 5)]
        public async Task InvokeAsync_WhenHopCountReachesOrExceedsMax_AndBehaviorIsDrop_ShortCircuits(int maxHops, int currentHops) {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = maxHops, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, currentHops.ToString());

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Fact]
        public async Task InvokeAsync_WhenHopCountExceedsMax_AndBehaviorIsThrow_ThrowsException() {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 3, Behavior = LoopDetectedBehavior.ThrowException };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "3");

            WebhookDelegate next = (ctx, ct) => Task.CompletedTask;

            // Act & Assert
            await Assert.ThrowsAsync<WebhookLoopDetectedException>(() =>
                middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken));
        }
    }

    public sealed class TheCausalCycleEvaluation {

        [Fact]
        public async Task InvokeAsync_WhenCausalChainContainsSelfInstanceId_ShortCircuits() {
            // Arrange
            LoopDetectionOptions options = new() {
                MaxHops = 10,
                InstanceId = "node-alpha",
                TrackCausalChain = true,
                Behavior = LoopDetectedBehavior.DropAndLog
            };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, "node-beta, node-alpha, node-gamma");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Theory]
        [InlineData("\"node-alpha\", \"node-beta\"")]
        [InlineData("  \"node-alpha\"  ,  \"node-beta\"  ")]
        [InlineData("node-beta, \"node-alpha\"")]
        [InlineData("\"NODE-ALPHA\"")]
        public async Task InvokeAsync_WhenCausalChainContainsQuotedOrMixedCaseTokens_DetectsCycle(string chainWithQuotes) {
            // Arrange
            LoopDetectionOptions options = new() {
                InstanceId = "node-alpha",
                TrackCausalChain = true,
                Behavior = LoopDetectedBehavior.DropAndLog
            };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, chainWithQuotes);

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Fact]
        public async Task InvokeAsync_WhenInstanceIdIsSubstringOfAnotherNode_DoesNotTriggerFalsePositive() {
            // Arrange: Current node is "node1", chain has "node10" and "node100" -> MUST NOT false-positive!
            LoopDetectionOptions options = new() {
                InstanceId = "node1",
                TrackCausalChain = true,
                MaxHops = 10,
                Behavior = LoopDetectedBehavior.DropAndLog
            };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, "node10, node100, node11");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("node10, node100, node11, node1", context.GetHeader(WebhookHeaderNames.WebhookCausalChain));
        }

        [Theory]
        [InlineData(",,, node-target ,,, ")]
        [InlineData(", node-x , ,, node-target , ")]
        [InlineData("node-target, ")]
        [InlineData(", node-target")]
        public async Task InvokeAsync_WhenCausalChainHasIrregularSeparatorsAndSpaces_ParsesCleanly(string dirtyChain) {
            // Arrange
            LoopDetectionOptions options = new() {
                InstanceId = "node-target",
                TrackCausalChain = true,
                Behavior = LoopDetectedBehavior.DropAndLog
            };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, dirtyChain);

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }
    }

    public sealed class TheHeaderFoldingResilience {

        [Fact]
        public async Task InvokeAsync_WhenHopHeaderIsFoldedByProxy_AndLastValueExceedsMax_ShortCircuits() {
            // Arrange: Proxy folded "1, 2, 5" where max is 4
            LoopDetectionOptions options = new() { MaxHops = 4, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1, 2, 5");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Fact]
        public async Task InvokeAsync_WhenHopHeaderIsFolded_AndValuesAreBelowMax_IncrementsToNextHop() {
            // Arrange: Proxy folded "1, 2" where max is 5 -> next hop is 3
            LoopDetectionOptions options = new() { MaxHops = 5, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1, 2");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("3", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }
    }

    public sealed class TheTamperingAndMalformedInputResilience {

        [Theory]
        [InlineData("-1")]
        [InlineData("-50")]
        [InlineData("-2147483648")]
        public async Task InvokeAsync_WhenHopHeaderIsNegative_TreatsAsZero_AndSetsNextHopToOne(string negativeHops) {
            // Arrange: Negative input spoofing
            LoopDetectionOptions options = new() { MaxHops = 5, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, negativeHops);

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("1", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }

        [Fact]
        public async Task InvokeAsync_WhenHopCountHeaderIsMalformed_TreatsAsInitialHopAndContinues() {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 5, InstanceId = "node-1" };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "malformed_string_xyz");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal("1", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }
    }

    public sealed class TheLargeCausalChainEvaluation {

        [Fact]
        public async Task InvokeAsync_WhenCausalChainHasHundredsOfNodes_DetectsCycleEfficiently() {
            // Arrange: 250 node chain with target in the middle
            List<string> nodes = [];
            for(int i = 0; i < 250; i++) {
                nodes.Add($"cluster-gateway-worker-instance-node-{i:D4}");
            }
            nodes.Insert(125, "MY-TARGET-NODE");
            string hugeChain = string.Join(", ", nodes);

            LoopDetectionOptions options = new() {
                InstanceId = "my-target-node",
                TrackCausalChain = true,
                MaxHops = 500,
                Behavior = LoopDetectedBehavior.DropAndLog
            };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");
            context.SetHeader(WebhookHeaderNames.WebhookCausalChain, hugeChain);

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }
    }

    public sealed class TheIntegerBoundaryHandling {

        [Fact]
        public async Task InvokeAsync_WhenHopCountIsIntMax_ShortCircuitsWithoutOverflow() {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 10, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, int.MaxValue.ToString());

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.True(context.TryGetResult(out WebhookDeliveryResult? result));
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(result);
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Theory]
        [InlineData("007", 7)]
        [InlineData("+3", 3)]
        public async Task InvokeAsync_WhenHopCountHasPaddedZeroesOrPlusSign_ParsesCorrectly(string validHopStr, int expectedParsed) {
            // Arrange
            LoopDetectionOptions options = new() { MaxHops = 10, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, validHopStr);

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal((expectedParsed + 1).ToString(), context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }
    }

    public sealed class TheArgumentValidation {

        [Fact]
        public async Task InvokeAsync_WhenArgumentsNull_ThrowsPrecaException() {
            // Arrange
            LoopDetectionOptions options = new();
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();

            // Act & Assert
            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(null!, (ctx, ct) => Task.CompletedTask, TestContext.Current.CancellationToken));

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                middleware.InvokeAsync(context, null!, TestContext.Current.CancellationToken));
        }
    }

    public sealed class TheDistributedTracingAndMetricsEvaluation {

        [Fact]
        public async Task InvokeAsync_WhenLoopDetected_TagsCurrentActivityAndEmitsSpanEvent() {
            // Arrange
            using ActivitySource activitySource = new("Wiaoj.Webhooks.Test.LoopTracing");
            using ActivityListener listener = new() {
                ShouldListenTo = s => s.Name == "Wiaoj.Webhooks.Test.LoopTracing",
                Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(listener);

            using Activity? activity = activitySource.StartActivity("test.delivery.loop");
            Assert.NotNull(activity);

            LoopDetectionOptions options = new() { MaxHops = 3, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "5");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.False(nextInvoked);
            Assert.Equal(ActivityStatusCode.Error, activity.Status);
            Assert.Equal(true, activity.GetTagItem("webhook.loop_detected"));
            Assert.Equal(5, activity.GetTagItem("webhook.hop_count"));

            ActivityEvent loopEvent = Assert.Single(activity.Events, e => e.Name == "webhook.loop_detected");
            Assert.Contains("Exceeded maximum allowable hop count of 3", loopEvent.Tags.First(t => t.Key == "reason").Value?.ToString());
        }

        [Fact]
        public async Task InvokeAsync_WhenHopIncremented_TagsCurrentActivityWithNextHop() {
            // Arrange
            using ActivitySource activitySource = new("Wiaoj.Webhooks.Test.HopTracing");
            using ActivityListener listener = new() {
                ShouldListenTo = s => s.Name == "Wiaoj.Webhooks.Test.HopTracing",
                Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded
            };
            ActivitySource.AddActivityListener(listener);

            using Activity? activity = activitySource.StartActivity("test.delivery.hop");
            Assert.NotNull(activity);

            LoopDetectionOptions options = new() { MaxHops = 10, Behavior = LoopDetectedBehavior.DropAndLog };
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);

            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "2");

            bool nextInvoked = false;
            WebhookDelegate next = (ctx, ct) => {
                nextInvoked = true;
                return Task.CompletedTask;
            };

            // Act
            await middleware.InvokeAsync(context, next, TestContext.Current.CancellationToken);

            // Assert
            Assert.True(nextInvoked);
            Assert.Equal(3, activity.GetTagItem("webhook.hop_count"));
        }
    }
}
