using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using System.Collections.Concurrent;
using Wiaoj.Security;
using Wiaoj.Security.Testing;
using Wiaoj.Serialization;
using Wiaoj.Serialization.SystemTextJson;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Retries;
using Wiaoj.Webhooks.Transports.InMemory;
using Xunit;

namespace Wiaoj.Webhooks.Tests.Integration.Recovery;

[Trait("Category", "Integration")]
[Trait("Feature", "Recovery")]
public sealed class StaleJobRecoveryIntegrationTests {
    private readonly FakeSecretProtector<WebhookSigningContext> _secretProtector = new();
    private readonly ConcurrentDictionary<string, int> _endpointAttemptCounters = new();

    [Fact]
    public async Task SweepAndRecoverAsync_RecoversOrphanedRetryingJob_AfterPodCrashAndRestart() {
        CancellationToken ct = TestContext.Current.CancellationToken;

        // 1. Arrange Shared State across Pods
        FakeTimeProvider timeProvider = new();
        DateTimeOffset now = new(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
        timeProvider.SetUtcNow(now);

        InMemoryWebhookStore sharedStore = new(timeProvider);
        InMemoryTestEndpointResolver endpointResolver = new();

        WebhookEndpointId endpointId = new("ep_recovery_test_01");
        Uri targetUri = new("http://localhost/api/sim-receiver/ep_recovery_test_01");
        EncryptedSecret<WebhookSigningContext> secret = this._secretProtector.Protect("whsec_recovery_secret_123");

        WebhookEndpoint endpoint = new(endpointId, targetUri, secret);
        endpointResolver.Register(endpoint);

        // ────────────────────────────────────────────────────────────────────
        // 2. POD 1: Boots up, processes first attempt, encounters 503, schedules retry
        // ────────────────────────────────────────────────────────────────────
        WebApplication appPod1 = CreateApp(
            timeProvider,
            sharedStore,
            endpointResolver,
            instanceId: "k8s-pod-1");

        await appPod1.StartAsync(ct);

        IWebhookDispatcher dispatcherPod1 = appPod1.Services.GetRequiredService<IWebhookDispatcher>();
        RecoveryTestEvent domainEvent = new("ORD-CRASH-41", 99.50m);

        // Dispatch webhook
        WebhookDeliveryHandle handle = await dispatcherPod1.DispatchAsync(endpointId, domainEvent, cancellationToken: ct);

        // Wait until Pod 1 marks the job as Retrying
        WebhookJobRecord? retryingJob = await WaitForJobStatusAsync(sharedStore, handle.JobId, WebhookJobStatus.Retrying, TimeSpan.FromSeconds(5), ct);
        Assert.NotNull(retryingJob);
        Assert.Equal(WebhookJobStatus.Retrying, retryingJob.Status);
        Assert.NotNull(retryingJob.NextAttemptAt);
        Assert.Single(retryingJob.Attempts);
        Assert.False(retryingJob.Attempts[0].IsSuccess);

        // ────────────────────────────────────────────────────────────────────
        // 3. POD 1 CRASH SIMULATION: Pod 1 stops abruptly (OOM kill / node restart).
        //    All in-memory delayed timers and transport channels vanish!
        // ────────────────────────────────────────────────────────────────────
        await appPod1.StopAsync(ct);
        await appPod1.DisposeAsync();

        // ────────────────────────────────────────────────────────────────────
        // 4. TIME PASSES: We advance time past the scheduled NextAttemptAt
        // ────────────────────────────────────────────────────────────────────
        timeProvider.Advance(TimeSpan.FromSeconds(10));

        // ────────────────────────────────────────────────────────────────────
        // 5. POD 2: New Pod boots up with the same shared store
        // ────────────────────────────────────────────────────────────────────
        WebApplication appPod2 = CreateApp(
            timeProvider,
            sharedStore,
            endpointResolver,
            instanceId: "k8s-pod-2");

        await appPod2.StartAsync(ct);

        // StaleJobRecoveryService on Pod 2 performs a recovery sweep
        StaleJobRecoveryService recoveryServicePod2 = appPod2.Services.GetRequiredService<StaleJobRecoveryService>();
        int recovered = await recoveryServicePod2.SweepAndRecoverAsync(ct);

        Assert.Equal(1, recovered);

        // ────────────────────────────────────────────────────────────────────
        // 6. POD 2 CONSUMER: Executes the recovered job from Pod 2's transport
        //    Endpoint now returns HTTP 200 OK -> Job delivers successfully!
        // ────────────────────────────────────────────────────────────────────
        WebhookJobRecord? deliveredJob = await WaitForJobStatusAsync(sharedStore, handle.JobId, WebhookJobStatus.Delivered, TimeSpan.FromSeconds(5), ct);

        Assert.NotNull(deliveredJob);
        Assert.Equal(WebhookJobStatus.Delivered, deliveredJob.Status);
        Assert.Equal(2, deliveredJob.Attempts.Count);

        // Attempt 1 was the 503 transient failure on Pod 1
        Assert.False(deliveredJob.Attempts[0].IsSuccess);
        Assert.IsType<WebhookDeliveryResult.TransientFailure>(deliveredJob.Attempts[0].Result);

        // Attempt 2 was the recovered successful delivery on Pod 2
        Assert.True(deliveredJob.Attempts[1].IsSuccess);
        Assert.IsType<WebhookDeliveryResult.Delivered>(deliveredJob.Attempts[1].Result);

        // Cleanup Pod 2
        await appPod2.StopAsync(ct);
        await appPod2.DisposeAsync();
    }

    private WebApplication CreateApp(
        TimeProvider timeProvider,
        IWebhookStore store,
        IWebhookEndpointResolver endpointResolver,
        string instanceId) {

        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<ISerializer<WebhookSerializerKey>, SystemTextJsonSerializer<WebhookSerializerKey>>();
        builder.Services.AddSingleton<ISecretProtector<WebhookSigningContext>>(this._secretProtector);
        builder.Services.AddSingleton(timeProvider);
        builder.Services.AddSingleton(endpointResolver);

        builder.Services.Configure<WebhookOptions>(opts => {
            opts.InstanceId = instanceId;
        });

        builder.Services.AddWiaojWebhooks(webhooks => {
            webhooks.UseStore(store)
                    .UseInMemoryTransport(options => {
                        options.Concurrency = 1;
                    })
                    .AllowPrivateNetworks()
                    .UseExponentialBackoffRetry(new ExponentialBackoffOptions {
                        MaxAttempts = 3,
                        InitialDelay = TimeSpan.FromSeconds(5),
                        Multiplier = 1.0,
                        Jitter = null
                    })
                    .UseHmacSha256Signing()
                    .RegisterEvent<RecoveryTestEvent>();
        });

        // Add StaleJobRecoveryService explicitly to DI so it can be resolved and triggered in test
        builder.Services.AddSingleton<StaleJobRecoveryService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<StaleJobRecoveryService>());

        builder.Services.AddHttpClient<HttpWebhookSender>()
            .ConfigurePrimaryHttpMessageHandler(sp => {
                TestServer server = (TestServer)sp.GetRequiredService<IServer>();
                return server.CreateHandler();
            });

        WebApplication app = builder.Build();

        app.MapPost("/api/sim-receiver/{endpointId}", (string endpointId, HttpRequest request) => {
            int attemptCount = this._endpointAttemptCounters.AddOrUpdate(endpointId, 1, (_, current) => current + 1);

            // Attempt 1 fails with HTTP 503 Transient Failure
            if(attemptCount == 1) {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            // Attempt 2 succeeds with HTTP 200
            return Results.Ok(new { Status = "Accepted", Attempt = attemptCount });
        });

        return app;
    }

    private static async Task<WebhookJobRecord?> WaitForJobStatusAsync(
        IWebhookStore store,
        WebhookJobId jobId,
        WebhookJobStatus targetStatus,
        TimeSpan timeout,
        CancellationToken ct) {

        using CancellationTokenSource timeoutCts = new(timeout);
        using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        while(!linkedCts.Token.IsCancellationRequested) {
            WebhookJobRecord? job = await store.GetJobAsync(jobId, linkedCts.Token);
            if(job is not null && job.Status == targetStatus) {
                return job;
            }

            await Task.Delay(50, linkedCts.Token);
        }

        return await store.GetJobAsync(jobId, ct);
    }

    public sealed record RecoveryTestEvent(string OrderId, decimal Amount) : IWebhookEvent;

    private sealed class InMemoryTestEndpointResolver : IWebhookEndpointResolver {
        private readonly ConcurrentDictionary<WebhookEndpointId, WebhookEndpoint> _endpoints = new();

        public void Register(WebhookEndpoint endpoint) {
            this._endpoints[endpoint.Id] = endpoint;
        }

        public ValueTask<WebhookEndpoint?> ResolveAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
            return ValueTask.FromResult(this._endpoints.GetValueOrDefault(endpointId));
        }
    }
}
