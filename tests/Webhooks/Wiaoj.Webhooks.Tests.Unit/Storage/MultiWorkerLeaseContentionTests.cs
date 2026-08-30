using System.Collections.Concurrent;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Storage;

[Trait("Category", "Unit")]
[Trait("Feature", "Persistence")]
[Trait("Component", "LeaseLocking")]
public sealed class MultiWorkerLeaseContentionTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. SINGLE JOB CONCURRENCY AND ATOMICITY
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSingleJobContention {
        [Fact]
        public async Task TryClaimLeaseAsync_UnderMassive50PodContention_AllowsExactlyOnePodToWin() {
            // Arrange: 50 pods compete simultaneously to claim a single stale job
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-alpha");

            WebhookJobRecord staleRecord = new(
                jobId,
                endpointId,
                "order.created",
                "{}",
                DateTimeOffset.UtcNow.AddMinutes(-10)) {
                Status = WebhookJobStatus.InFlight,
                LockedBy = "crashed-worker-pod",
                LockExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-3) // Expired lock
            };
            await store.SaveAsync(staleRecord, TestContext.Current.CancellationToken);

            const int podCount = 50;
            int successfulClaimCount = 0;
            ConcurrentBag<string> winningPods = [];

            // Act: 50 Tasks compete concurrently (Thread-pool flood)
            Task[] tasks = [.. Enumerable.Range(0, podCount).Select(async i => {
                string podId = $"worker-pod-{i:D2}";
                bool claimed = await store.TryClaimLeaseAsync(jobId, podId, TimeSpan.FromMinutes(2));
                if(claimed) {
                    Interlocked.Increment(ref successfulClaimCount);
                    winningPods.Add(podId);
                }
            })];

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, successfulClaimCount);
            string item = Assert.Single(winningPods);

            WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal(WebhookJobStatus.InFlight, updated.Status);
            Assert.Equal(item, updated.LockedBy);
            Assert.True(updated.LockExpiresAt > DateTimeOffset.UtcNow);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. MULTI-JOB AND MULTI-POD DISTRIBUTED MATRIX CONTENTION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheMultiJobContentionMatrix {
        [Fact]
        public async Task TryClaimLeaseAsync_With20PodsAnd50StaleJobs_DistributesAllJobsWithZeroOverlap() {
            // Arrange: 50 stale jobs created
            InMemoryWebhookStore store = new();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("cluster-node");
            List<WebhookJobId> jobIds = [];

            for(int i = 0; i < 50; i++) {
                WebhookJobId jobId = WebhookJobId.NewJobId();
                jobIds.Add(jobId);
                WebhookJobRecord record = new(jobId, endpointId, $"event.{i}", "{}", DateTimeOffset.UtcNow.AddMinutes(-10)) {
                    Status = WebhookJobStatus.InFlight,
                    LockExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
                };
                await store.SaveAsync(record, TestContext.Current.CancellationToken);
            }

            const int podCount = 20;
            ConcurrentDictionary<WebhookJobId, string> claimedMap = new();

            // Act: 20 pods attempt to claim all 50 jobs concurrently
            Task[] tasks = [.. Enumerable.Range(0, podCount).Select(podIndex => {
                string podId = $"pod-{podIndex}";
                return Task.Run(async () => {
                    foreach(WebhookJobId id in jobIds) {
                        bool won = await store.TryClaimLeaseAsync(id, podId, TimeSpan.FromMinutes(1));
                        if(won) {
                            bool added = claimedMap.TryAdd(id, podId);
                            Assert.True(added, $"CRITICAL RACE: Job {id} was claimed by multiple pods!");
                        }
                    }
                });
            })];

            await Task.WhenAll(tasks);

            // Assert: All 50 jobs must be won by exactly 1 pod
            Assert.Equal(50, claimedMap.Count);
            foreach(WebhookJobId id in jobIds) {
                Assert.True(claimedMap.ContainsKey(id));
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. LEASE RENEWAL AND EXPIRATION TRANSITION MECHANISMS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheLeaseLifecycleAndTransitions {
        [Fact]
        public async Task TryClaimLeaseAsync_AllowsSamePodToRenew_WhileBlockingOthers() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "order.paid", "{}", DateTimeOffset.UtcNow);
            await store.SaveAsync(record, TestContext.Current.CancellationToken);

            // 1. Pod-1 locks job for 10 minutes
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken));

            // 2. Pod-2 attempts to interfere -> Must fail
            Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(10), TestContext.Current.CancellationToken));

            // 3. Pod-1 extends own lease (Heartbeat / Renew) -> Must succeed
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(30), TestContext.Current.CancellationToken));

            WebhookJobRecord? updated = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
            Assert.NotNull(updated);
            Assert.Equal("pod-1", updated.LockedBy);
            Assert.True(updated.LockExpiresAt > DateTimeOffset.UtcNow.AddMinutes(25));
        }

        [Fact]
        public async Task TryClaimLeaseAsync_WhenLeaseExpires_AllowsAnotherPodToTakeOver() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "order.paid", "{}", DateTimeOffset.UtcNow);
            await store.SaveAsync(record, TestContext.Current.CancellationToken);

            // 1. Pod-1 takes an immediately expiring lease (0ms)
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.Zero, TestContext.Current.CancellationToken));

            // 2. Pod-2 claims expired lease -> Must succeed
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));

            // 3. Previous owner Pod-1 can no longer mutate -> Must be rejected
            Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));

            WebhookJobRecord? finalRecord = await store.GetJobAsync(jobId, TestContext.Current.CancellationToken);
            Assert.NotNull(finalRecord);
            Assert.Equal("pod-2", finalRecord.LockedBy);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. NEGATIVE AND BOUNDARY GUARDS
    // ────────────────────────────────────────────────────────────────────────

    public sealed class NegativeAndBoundaryGuards {
        [Fact]
        public async Task TryClaimLeaseAsync_ReturnsFalse_WhenJobDoesNotExist() {
            InMemoryWebhookStore store = new();
            WebhookJobId ghostJobId = WebhookJobId.NewJobId();

            bool claimed = await store.TryClaimLeaseAsync(ghostJobId, "pod-1", TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);

            Assert.False(claimed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task TryClaimLeaseAsync_Throws_WhenInstanceIdIsInvalid(string? invalidInstanceId) {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();

            await Assert.ThrowsAnyAsync<ArgumentException>(() =>
                store.TryClaimLeaseAsync(jobId, invalidInstanceId!, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task TryClaimLeaseAsync_Throws_WhenDurationIsNegative() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() =>
                store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromSeconds(-1), TestContext.Current.CancellationToken));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. STALE JOB FILTRATION ACCURACY
    // ────────────────────────────────────────────────────────────────────────

    public sealed class StaleFiltrationAccuracy {
        [Fact]
        public async Task GetStaleInFlightJobsAsync_ExcludesDeliveredQueuedAndActiveLeases() {
            // Arrange
            InMemoryWebhookStore store = new();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // 1. Truly stale job (InFlight + expired 5 minutes ago)
            WebhookJobRecord stale = new(WebhookJobId.NewJobId(), endpointId, "e.stale", "{}", now) {
                Status = WebhookJobStatus.InFlight,
                LockExpiresAt = now.AddMinutes(-5)
            };

            // 2. Active in-flight job (InFlight + expires in 5 minutes) -> EXCLUDED
            WebhookJobRecord activeInFlight = new(WebhookJobId.NewJobId(), endpointId, "e.active", "{}", now) {
                Status = WebhookJobStatus.InFlight,
                LockExpiresAt = now.AddMinutes(5)
            };

            // 3. Delivered job -> EXCLUDED
            WebhookJobRecord delivered = new(WebhookJobId.NewJobId(), endpointId, "e.delivered", "{}", now) {
                Status = WebhookJobStatus.Delivered,
                LockExpiresAt = now.AddMinutes(-5)
            };

            // 4. Queued job -> EXCLUDED
            WebhookJobRecord queued = new(WebhookJobId.NewJobId(), endpointId, "e.queued", "{}", now) {
                Status = WebhookJobStatus.Queued,
                LockExpiresAt = now.AddMinutes(-5)
            };

            await store.SaveAsync(stale, TestContext.Current.CancellationToken);
            await store.SaveAsync(activeInFlight, TestContext.Current.CancellationToken);
            await store.SaveAsync(delivered, TestContext.Current.CancellationToken);
            await store.SaveAsync(queued, TestContext.Current.CancellationToken);

            // Act
            IReadOnlyList<WebhookJobRecord> staleList = await store.GetExpiredInFlightJobsAsync(now, maxCount: 10, TestContext.Current.CancellationToken);

            // Assert: List must contain only the single truly stale job
            WebhookJobRecord item = Assert.Single(staleList);
            Assert.Equal(stale.Id, item.Id);
        }
    }
}