using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.LoopDetection;
using Wiaoj.Webhooks.Tests.Unit.Fakes;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.LoopDetection;

/// <summary>
/// The causality of an inbound webhook must travel with every job published while handling it: through the dispatcher,
/// the store, the transport, the job handler, retries, replay and recovery, until the loop detection middleware reads it.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "LoopDetection")]
[Trait("Component", "Causality")]
public sealed class CausalityPropagationTests {

    public sealed class TheMiddleware {

        private static async Task<(bool NextInvoked, WebhookDeliveryContext Context)> RunAsync(LoopDetectionOptions options, WebhookCausality inherited) {
            LoopDetectionMiddleware middleware = new(options, NullLogger<LoopDetectionMiddleware>.Instance);
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetCausality(inherited);

            bool nextInvoked = false;
            await middleware.InvokeAsync(context, (_, _) => {
                nextInvoked = true;
                return Task.CompletedTask;
            }, TestContext.Current.CancellationToken);

            return (nextInvoked, context);
        }

        [Fact]
        public async Task InvokeAsync_ExtendsTheInheritedHopCountAndChain() {
            (bool nextInvoked, WebhookDeliveryContext context) = await RunAsync(
                new LoopDetectionOptions { ServiceName = "svc-c", MaxHops = 5 },
                new WebhookCausality(2, "svc-a, svc-b"));

            Assert.True(nextInvoked);
            Assert.Equal("3", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
            Assert.Equal("svc-a, svc-b, svc-c", context.GetHeader(WebhookHeaderNames.WebhookCausalChain));
        }

        [Fact]
        public async Task InvokeAsync_WhenTheInheritedChainContainsThisService_DropsTheDelivery() {
            (bool nextInvoked, WebhookDeliveryContext context) = await RunAsync(
                new LoopDetectionOptions { ServiceName = "svc-a", MaxHops = 10 },
                new WebhookCausality(2, "svc-a, svc-b"));

            Assert.False(nextInvoked);
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(context.GetResult());
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Fact]
        public async Task InvokeAsync_WhenTheInheritedHopCountReachedMaxHops_DropsTheDelivery() {
            (bool nextInvoked, WebhookDeliveryContext context) = await RunAsync(
                new LoopDetectionOptions { ServiceName = "svc-c", MaxHops = 3, TrackCausalChain = false },
                new WebhookCausality(3, null));

            Assert.False(nextInvoked);
            WebhookDeliveryResult.PermanentFailure failure = Assert.IsType<WebhookDeliveryResult.PermanentFailure>(context.GetResult());
            Assert.Equal(PermanentFailureReason.LoopDetected, failure.Reason);
        }

        [Fact]
        public async Task InvokeAsync_UsesTheHigherOfTheInheritedAndHeaderHopCounts() {
            LoopDetectionMiddleware middleware = new(new LoopDetectionOptions { ServiceName = "svc-c", MaxHops = 10 });
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetCausality(new WebhookCausality(4, "svc-a"));
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "1");

            await middleware.InvokeAsync(context, (_, _) => Task.CompletedTask, TestContext.Current.CancellationToken);

            Assert.Equal("5", context.GetHeader(WebhookHeaderNames.WebhookHopCount));
        }

        [Fact]
        public async Task InvokeAsync_WhenDisabled_PassesThroughWithoutHeaders_EvenForALoop() {
            (bool nextInvoked, WebhookDeliveryContext context) = await RunAsync(
                new LoopDetectionOptions { ServiceName = "svc-a", MaxHops = 1, Enabled = false },
                new WebhookCausality(9, "svc-a"));

            Assert.True(nextInvoked);
            Assert.False(context.TryGetResult(out _));
            Assert.False(context.HasHeader(WebhookHeaderNames.WebhookHopCount));
            Assert.False(context.HasHeader(WebhookHeaderNames.WebhookCausalChain));
        }
    }

    public sealed class TheRegistration {

        private static ServiceCollection CreateServices() {
            ServiceCollection services = new();
            services.AddLogging();
            return services;
        }

        [Fact]
        public void AddWiaojWebhooks_RegistersLoopDetectionOnByDefault_AsTheFirstMiddleware() {
            ServiceCollection services = CreateServices();
            services.AddWiaojWebhooks().AddMiddleware(new ShortCircuitingWebhookMiddleware([]));

            using ServiceProvider provider = services.BuildServiceProvider();
            IWebhookMiddleware[] middleware = [.. provider.GetServices<IWebhookMiddleware>()];

            Assert.IsType<LoopDetectionMiddleware>(middleware[0]);
            Assert.True(provider.GetRequiredService<LoopDetectionOptions>().Enabled);
        }

        [Fact]
        public void AddWiaojWebhooks_CalledTwice_RegistersTheMiddlewareOnce() {
            ServiceCollection services = CreateServices();
            services.AddWiaojWebhooks();
            services.AddWiaojWebhooks().UseLoopDetection().UseLoopDetection(7);

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.Single(provider.GetServices<IWebhookMiddleware>().OfType<LoopDetectionMiddleware>());
            Assert.Equal(7, provider.GetRequiredService<LoopDetectionOptions>().MaxHops);
        }

        [Fact]
        public void DisableLoopDetection_TurnsTheMiddlewareOff() {
            ServiceCollection services = CreateServices();
            services.AddWiaojWebhooks().DisableLoopDetection();

            using ServiceProvider provider = services.BuildServiceProvider();

            Assert.False(provider.GetRequiredService<LoopDetectionOptions>().Enabled);
        }

        [Fact]
        public void UseLoopDetection_WithAnOptionsInstance_CopiesEveryOption() {
            ServiceCollection services = CreateServices();
            LoopDetectionOptions custom = new() {
                Enabled = false,
                MaxHops = 9,
                ServiceName = "billing",
                TrackCausalChain = false,
                HopCountHeaderName = "X-Hops",
                CausalChainHeaderName = "X-Chain",
                Behavior = LoopDetectedBehavior.ThrowException
            };
            services.AddWiaojWebhooks().UseLoopDetection(custom);

            using ServiceProvider provider = services.BuildServiceProvider();
            LoopDetectionOptions options = provider.GetRequiredService<LoopDetectionOptions>();

            Assert.False(options.Enabled);
            Assert.Equal(9, options.MaxHops);
            Assert.Equal("billing", options.ServiceName);
            Assert.False(options.TrackCausalChain);
            Assert.Equal("X-Hops", options.HopCountHeaderName);
            Assert.Equal("X-Chain", options.CausalChainHeaderName);
            Assert.Equal(LoopDetectedBehavior.ThrowException, options.Behavior);
        }
    }

    public sealed class TheDispatcher {

        [Fact]
        public async Task DispatchAsync_InsideAScope_StoresTheCausalityOnTheRecordAndTheJob() {
            InMemoryWebhookStore store = new();
            FakeWebhookTransport transport = new();
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(store, transport);

            WebhookDeliveryHandle handle;
            using(WebhookCausalityScope.Begin(new WebhookCausality(2, "svc-a, svc-b"))) {
                handle = await dispatcher.DispatchAsync(WebhookTestFactory.CreateEndpointId(), WebhookTestFactory.CreateEvent(), TestContext.Current.CancellationToken);
            }

            WebhookJobRecord? record = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
            Assert.NotNull(record);
            Assert.Equal(2, record.HopCount);
            Assert.Equal("svc-a, svc-b", record.CausalChain);

            WebhookDeliveryJob job = Assert.Single(transport.EnqueuedJobs).Job;
            Assert.Equal(2, job.HopCount);
            Assert.Equal("svc-a, svc-b", job.CausalChain);
        }

        [Fact]
        public async Task DispatchAsync_OutsideAnyScope_StartsANewChain() {
            InMemoryWebhookStore store = new();
            FakeWebhookTransport transport = new();
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(store, transport);

            WebhookDeliveryHandle handle = await dispatcher.DispatchAsync(WebhookTestFactory.CreateEndpointId(), WebhookTestFactory.CreateEvent(), TestContext.Current.CancellationToken);

            WebhookJobRecord? record = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
            Assert.NotNull(record);
            Assert.Equal(0, record.HopCount);
            Assert.Null(record.CausalChain);
            Assert.Equal(0, Assert.Single(transport.EnqueuedJobs).Job.HopCount);
        }

        [Fact]
        public async Task DispatchBatchAsync_InsideAScope_StoresTheCausalityOnEveryRecordAndJob() {
            InMemoryWebhookStore store = new();
            FakeWebhookTransport transport = new();
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(store, transport);

            IReadOnlyList<WebhookDeliveryHandle> handles;
            using(WebhookCausalityScope.Begin(new WebhookCausality(1, "svc-a"))) {
                handles = await dispatcher.DispatchBatchAsync(
                    WebhookTestFactory.CreateEndpointId(),
                    [WebhookTestFactory.CreateEvent("ORD-1"), WebhookTestFactory.CreateEvent("ORD-2")],
                    TestContext.Current.CancellationToken);
            }

            foreach(WebhookDeliveryHandle handle in handles) {
                WebhookJobRecord? record = await store.GetJobAsync(handle.JobId, TestContext.Current.CancellationToken);
                Assert.NotNull(record);
                Assert.Equal(1, record.HopCount);
                Assert.Equal("svc-a", record.CausalChain);
            }

            Assert.All(transport.EnqueuedJobs, entry => {
                Assert.Equal(1, entry.Job.HopCount);
                Assert.Equal("svc-a", entry.Job.CausalChain);
            });
        }

        [Fact]
        public async Task ReplayAsync_CarriesTheStoredCausality() {
            InMemoryWebhookStore store = new();
            FakeWebhookTransport transport = new();
            WebhookDispatcher dispatcher = WebhookTestFactory.CreateDispatcher(store, transport);

            WebhookDeliveryHandle handle;
            using(WebhookCausalityScope.Begin(new WebhookCausality(3, "svc-a, svc-b, svc-a"))) {
                handle = await dispatcher.DispatchAsync(WebhookTestFactory.CreateEndpointId(), WebhookTestFactory.CreateEvent(), TestContext.Current.CancellationToken);
            }

            // The replay runs outside the scope, as an operator would trigger it.
            await dispatcher.ReplayAsync(handle.JobId, TestContext.Current.CancellationToken);

            WebhookDeliveryJob replayed = transport.EnqueuedJobs[^1].Job;
            Assert.True(replayed.IsReplay);
            Assert.Equal(3, replayed.HopCount);
            Assert.Equal("svc-a, svc-b, svc-a", replayed.CausalChain);
        }
    }

    public sealed class TheJobLifecycle {

        [Fact]
        public async Task HandleAsync_HandsTheJobCausalityToThePipeline() {
            FakeWebhookDeliverer deliverer = new();
            WebhookPipelineRunner runner = new([], deliverer, new FakeTimeProvider(), NullLogger<WebhookPipelineRunner>.Instance);
            WebhookJobHandler handler = new(
                new InMemoryWebhookStore(),
                new FakeWebhookEndpointResolver().Register(WebhookTestFactory.CreateEndpoint()),
                new FakeWebhookSerializer(),
                runner,
                new FakeTimeProvider(),
                NullLogger<WebhookJobHandler>.Instance);

            WebhookDeliveryJob job = WebhookTestFactory.CreateJob() with { HopCount = 2, CausalChain = "svc-a, svc-b" };

            await handler.HandleAsync(job, TestContext.Current.CancellationToken);

            WebhookCausality causality = Assert.Single(deliverer.ReceivedContexts).GetCausality();
            Assert.Equal(new WebhookCausality(2, "svc-a, svc-b"), causality);
        }

        [Fact]
        public void FromContext_CarriesTheCausalityIntoTheRetryJob() {
            WebhookDeliveryContext context = WebhookTestFactory.CreateContext();
            context.SetCausality(new WebhookCausality(2, "svc-a, svc-b"));

            // The middleware wrote this delivery's hop to the headers; the retry must start from the inherited values again.
            context.SetHeader(WebhookHeaderNames.WebhookHopCount, "3");

            WebhookDeliveryJob retry = WebhookDeliveryJob.FromContext(context);

            Assert.Equal(2, retry.HopCount);
            Assert.Equal("svc-a, svc-b", retry.CausalChain);
        }

        [Fact]
        public void HopCount_WhenNegative_Throws() {
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => WebhookTestFactory.CreateJob() with { HopCount = -1 });
            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => new WebhookJobRecord(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                "order.created",
                "{}",
                DateTimeOffset.UtcNow) { HopCount = -1 });
        }

        [Fact]
        public async Task SweepAndRecoverAsync_CarriesTheStoredCausality() {
            FakeTimeProvider timeProvider = new();
            DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
            timeProvider.SetUtcNow(now);

            InMemoryWebhookStore store = new(timeProvider);
            FakeWebhookTransport transport = new();

            WebhookJobRecord stale = new(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                "order.created",
                "{\"OrderId\":\"ORD-1\",\"Amount\":42.50}",
                now.AddMinutes(-10)) {
                Status = WebhookJobStatus.InFlight,
                LockedBy = "crashed-pod",
                LockExpiresAt = now.AddMinutes(-5),
                HopCount = 4,
                CausalChain = "svc-a, svc-b"
            };
            await store.SaveAsync(stale, TestContext.Current.CancellationToken);

            StaleJobRecoveryService service = WebhookTestFactory.CreateRecoveryService(store: store, transport: transport, timeProvider: timeProvider);

            Assert.Equal(1, await service.SweepAndRecoverAsync(TestContext.Current.CancellationToken));

            WebhookDeliveryJob recovered = Assert.Single(transport.EnqueuedJobs).Job;
            Assert.Equal(4, recovered.HopCount);
            Assert.Equal("svc-a, svc-b", recovered.CausalChain);
        }
    }
}
