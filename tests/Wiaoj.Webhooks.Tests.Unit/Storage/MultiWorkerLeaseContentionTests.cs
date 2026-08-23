using System.Collections.Concurrent;
using Wiaoj.Webhooks.Internal;
using Wiaoj.Webhooks.Tests.Unit.TestData;

namespace Wiaoj.Webhooks.Tests.Unit.Storage;

[Trait("Category", "Unit")]
[Trait("Feature", "Persistence")]
[Trait("Component", "LeaseLocking")]
public sealed class MultiWorkerLeaseContentionTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. TEKİL İŞ ÜZERİNDE ÇOKLU POD YARIŞI & ATOMİKLİK
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSingleJobContention {
        [Fact]
        public async Task TryClaimLeaseAsync_UnderMassive50PodContention_AllowsExactlyOnePodToWin() {
            // Arrange: 50 pod aynı anda tek bir stale işi kapmaya çalışır
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
                LockExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-3) // Süresi dolmuş
            };
            await store.SaveAsync(staleRecord);

            const int podCount = 50;
            int successfulClaimCount = 0;
            ConcurrentBag<string> winningPods = [];

            // Act: 50 Task aynı anda yarışır (Thread-pool flood)
            Task[] tasks = Enumerable.Range(0, podCount).Select(async i => {
                string podId = $"worker-pod-{i:D2}";
                bool claimed = await store.TryClaimLeaseAsync(jobId, podId, TimeSpan.FromMinutes(2));
                if(claimed) {
                    Interlocked.Increment(ref successfulClaimCount);
                    winningPods.Add(podId);
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(1, successfulClaimCount);
            Assert.Single(winningPods);

            WebhookJobRecord? updated = await store.GetJobAsync(jobId);
            Assert.NotNull(updated);
            Assert.Equal(WebhookJobStatus.InFlight, updated.Status);
            Assert.Equal(winningPods.First(), updated.LockedBy);
            Assert.True(updated.LockExpiresAt > DateTimeOffset.UtcNow);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. ÇOKLU İŞ & ÇOKLU POD DAĞITIK MATRİS YARIŞI
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheMultiJobContentionMatrix {
        [Fact]
        public async Task TryClaimLeaseAsync_With20PodsAnd50StaleJobs_DistributesAllJobsWithZeroOverlap() {
            // Arrange: 50 tane stale iş oluşturulur
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
                await store.SaveAsync(record);
            }

            const int podCount = 20;
            ConcurrentDictionary<WebhookJobId, string> claimedMap = new();

            // Act: 20 pod 50 işin tamamını eşzamanlı olarak kapmaya çalışır
            Task[] tasks = Enumerable.Range(0, podCount).Select(podIndex => {
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
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: 50 işin tamamı tam olarak 1 pod tarafından kazanılmış olmalı
            Assert.Equal(50, claimedMap.Count);
            foreach(WebhookJobId id in jobIds) {
                Assert.True(claimedMap.ContainsKey(id));
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. LEASE YENİLEME & EXPIRE DEVİR MEKANİZMASI
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheLeaseLifecycleAndTransitions {
        [Fact]
        public async Task TryClaimLeaseAsync_AllowsSamePodToRenew_WhileBlockingOthers() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "order.paid", "{}", DateTimeOffset.UtcNow);
            await store.SaveAsync(record);

            // 1. Pod-1 işi 10 dakikalığına kilitler
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(10)));

            // 2. Pod-2 araya girmeye çalışır -> Başarısız olmalı
            Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(10)));

            // 3. Pod-1 kendi lease'ini uzatır (Heartbeat / Renew) -> Başarılı olmalı
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(30)));

            WebhookJobRecord? updated = await store.GetJobAsync(jobId);
            Assert.NotNull(updated);
            Assert.Equal("pod-1", updated.LockedBy);
            Assert.True(updated.LockExpiresAt > DateTimeOffset.UtcNow.AddMinutes(25));
        }

        [Fact]
        public async Task TryClaimLeaseAsync_WhenLeaseExpires_AllowsAnotherPodToTakeOver() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();
            WebhookJobRecord record = new(jobId, WebhookTestFactory.CreateEndpointId(), "order.paid", "{}", DateTimeOffset.UtcNow);
            await store.SaveAsync(record);

            // 1. Pod-1 anında dolan (0ms) lease alır
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.Zero));

            // 2. Pod-2 süresi dolmuş lease'i devralır -> Başarılı olmalı
            Assert.True(await store.TryClaimLeaseAsync(jobId, "pod-2", TimeSpan.FromMinutes(5)));

            // 3. Eski sahibi Pod-1 artık işlem yapamaz -> Reddedilmeli
            Assert.False(await store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromMinutes(5)));

            WebhookJobRecord? finalRecord = await store.GetJobAsync(jobId);
            Assert.NotNull(finalRecord);
            Assert.Equal("pod-2", finalRecord.LockedBy);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. NEGATİF GİRİŞLER & BOUNDARY KONTROLLERİ
    // ────────────────────────────────────────────────────────────────────────

    public sealed class NegativeAndBoundaryGuards {
        [Fact]
        public async Task TryClaimLeaseAsync_ReturnsFalse_WhenJobDoesNotExist() {
            InMemoryWebhookStore store = new();
            WebhookJobId ghostJobId = WebhookJobId.NewJobId();

            bool claimed = await store.TryClaimLeaseAsync(ghostJobId, "pod-1", TimeSpan.FromMinutes(1));

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
                store.TryClaimLeaseAsync(jobId, invalidInstanceId!, TimeSpan.FromMinutes(1)));
        }

        [Fact]
        public async Task TryClaimLeaseAsync_Throws_WhenDurationIsNegative() {
            InMemoryWebhookStore store = new();
            WebhookJobId jobId = WebhookJobId.NewJobId();

            await Assert.ThrowsAnyAsync<ArgumentOutOfRangeException>(() =>
                store.TryClaimLeaseAsync(jobId, "pod-1", TimeSpan.FromSeconds(-1)));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. STALE İŞ FİLTRELEME DOĞRULUĞU
    // ────────────────────────────────────────────────────────────────────────

    public sealed class StaleFiltrationAccuracy {
        [Fact]
        public async Task GetStaleInFlightJobsAsync_ExcludesDeliveredQueuedAndActiveLeases() {
            // Arrange
            InMemoryWebhookStore store = new();
            WebhookEndpointId endpointId = WebhookTestFactory.CreateEndpointId("customer-1");
            DateTimeOffset now = DateTimeOffset.UtcNow;

            // 1. Gerçekten Stale olan (InFlight + süresi 5 dk önce dolmuş)
            WebhookJobRecord stale = new(WebhookJobId.NewJobId(), endpointId, "e.stale", "{}", now) {
                Status = WebhookJobStatus.InFlight,
                LockExpiresAt = now.AddMinutes(-5)
            };

            // 2. Aktif çalışan (InFlight + süresi 5 dk sonra dolacak) -> ALINMAMALI
            WebhookJobRecord activeInFlight = new(WebhookJobId.NewJobId(), endpointId, "e.active", "{}", now) {
                Status = WebhookJobStatus.InFlight,
                LockExpiresAt = now.AddMinutes(5)
            };

            // 3. Teslim edilmiş olan (Delivered + süresi geçmiş olsa bile) -> ALINMAMALI
            WebhookJobRecord delivered = new(WebhookJobId.NewJobId(), endpointId, "e.delivered", "{}", now) {
                Status = WebhookJobStatus.Delivered,
                LockExpiresAt = now.AddMinutes(-5)
            };

            // 4. Kuyrukta bekleyen (Queued) -> ALINMAMALI
            WebhookJobRecord queued = new(WebhookJobId.NewJobId(), endpointId, "e.queued", "{}", now) {
                Status = WebhookJobStatus.Queued,
                LockExpiresAt = now.AddMinutes(-5)
            };

            await store.SaveAsync(stale);
            await store.SaveAsync(activeInFlight);
            await store.SaveAsync(delivered);
            await store.SaveAsync(queued);

            // Act
            IReadOnlyList<WebhookJobRecord> staleList = await store.GetStaleInFlightJobsAsync(now, maxCount: 10);

            // Assert: Listede sadece ve sadece 1 numaralı stale iş bulunmalıdır
            Assert.Single(staleList);
            Assert.Equal(stale.Id, staleList[0].Id);
        }
    }
}