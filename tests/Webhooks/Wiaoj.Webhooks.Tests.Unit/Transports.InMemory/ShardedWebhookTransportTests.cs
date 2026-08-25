using Wiaoj.Primitives.Hashing;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Transports.InMemory;

[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "ShardedTransport")]
public sealed class ShardedWebhookTransportTests {

    // ────────────────────────────────────────────────────────────────────────
    // 1. CONSTRUCTOR, BOUNDS & DISPOSAL
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConstructorAndDisposal {
        [Fact]
        public void Constructor_Throws_WhenShardsArrayIsNullOrEmpty() {
            Assert.ThrowsAny<ArgumentNullException>(() => new ShardedWebhookTransport(null!));
            Assert.ThrowsAny<ArgumentException>(() => new ShardedWebhookTransport([]));
        }

        [Fact]
        public void Constructor_InitializesCorrectly_WithValidShards() {
            using InMemoryWebhookTransport shard1 = new();
            using InMemoryWebhookTransport shard2 = new();
            using ShardedWebhookTransport sharded = new([shard1, shard2]);

            Assert.Equal(2, sharded.ShardCount);
            Assert.Same(shard1, sharded.GetShard(0));
            Assert.Same(shard2, sharded.GetShard(1));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(2)]
        [InlineData(10)]
        public void GetShard_Throws_WhenIndexIsOutOfRange(int invalidIndex) {
            using InMemoryWebhookTransport shard1 = new();
            using InMemoryWebhookTransport shard2 = new();
            using ShardedWebhookTransport sharded = new([shard1, shard2]);

            Assert.ThrowsAny<ArgumentOutOfRangeException>(() => sharded.GetShard(invalidIndex));
        }

        [Fact]
        public void Dispose_CascadesDisposal_ToAllUnderlyingShards() {
            InMemoryWebhookTransport shard1 = new();
            InMemoryWebhookTransport shard2 = new();
            ShardedWebhookTransport sharded = new([shard1, shard2]);

            // Act
            sharded.Dispose();

            // Assert: Channels in underlying shards must be completed and closed
            Assert.False(shard1.Writer.TryWrite(WebhookTestFactory.CreateJob()));
            Assert.False(shard2.Writer.TryWrite(WebhookTestFactory.CreateJob()));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 2. SINGLE-JOB ROUTING & DETERMINISM (POWER-OF-TWO VS MODULO)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheSingleJobRoutingBehavior {
        [Fact]
        public async Task EnqueueAsync_WithPowerOfTwoShards_UsesBitmask_AndRoutesConsistently() {
            // Arrange: 8 shards (Power of Two -> Bitmask fast-path)
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new(), new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            const string partitionKey = "order-group-alpha";
            WebhookDeliveryJob job1 = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "order.created",
                WebhookTestFactory.CreateEvent());

            WebhookDeliveryJob job2 = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "order.paid",
                WebhookTestFactory.CreateEvent());

            // Act
            await sharded.EnqueueAsync(job1, TestContext.Current.CancellationToken);
            await sharded.EnqueueAsync(job2, TestContext.Current.CancellationToken);

            ulong hash = XxHash3.Compute(partitionKey.AsSpan()).Value;
            int expectedShardIndex = (int)(hash & 7ul);

            InMemoryWebhookTransport targetShard = (InMemoryWebhookTransport)sharded.GetShard(expectedShardIndex);

            // Assert: Strict FIFO sequence inside the target shard
            Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? readJob1));
            Assert.Same(job1, readJob1);

            Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? readJob2));
            Assert.Same(job2, readJob2);
        }

        [Fact]
        public async Task EnqueueAsync_WithNonPowerOfTwoShards_UsesModulo_AndRoutesConsistently() {
            // Arrange: 5 shards (Non-Power of Two -> Modulo path)
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            const string partitionKey = "tenant-billing-odd-shards";
            WebhookDeliveryJob job1 = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "invoice.created",
                WebhookTestFactory.CreateEvent());

            WebhookDeliveryJob job2 = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "invoice.finalized",
                WebhookTestFactory.CreateEvent());

            // Act
            await sharded.EnqueueAsync(job1, TestContext.Current.CancellationToken);
            await sharded.EnqueueAsync(job2, TestContext.Current.CancellationToken);

            ulong hash = XxHash3.Compute(partitionKey.AsSpan()).Value;
            int expectedShardIndex = (int)(hash % 5ul);

            InMemoryWebhookTransport targetShard = (InMemoryWebhookTransport)sharded.GetShard(expectedShardIndex);

            Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? readJob1));
            Assert.Same(job1, readJob1);

            Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? readJob2));
            Assert.Same(job2, readJob2);
        }

        [Fact]
        public async Task EnqueueAsync_IsDeterministic_AcrossSeparateTransportInstances() {
            // Arrange: 2 completely independent transport router instances
            using ShardedWebhookTransport routerInstance1 = new([new InMemoryWebhookTransport(), new InMemoryWebhookTransport(), new InMemoryWebhookTransport(), new InMemoryWebhookTransport()]);
            using ShardedWebhookTransport routerInstance2 = new([new InMemoryWebhookTransport(), new InMemoryWebhookTransport(), new InMemoryWebhookTransport(), new InMemoryWebhookTransport()]);

            const string partitionKey = "cross-restart-determinism-key";
            WebhookDeliveryJob job = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "test.event",
                WebhookTestFactory.CreateEvent());

            // Act
            await routerInstance1.EnqueueAsync(job, TestContext.Current.CancellationToken);
            await routerInstance2.EnqueueAsync(job, TestContext.Current.CancellationToken);

            int receivedIndex1 = -1;
            int receivedIndex2 = -1;

            for(int i = 0; i < 4; i++) {
                if(((InMemoryWebhookTransport)routerInstance1.GetShard(i)).Reader.TryRead(out _)) receivedIndex1 = i;
                if(((InMemoryWebhookTransport)routerInstance2.GetShard(i)).Reader.TryRead(out _)) receivedIndex2 = i;
            }

            // Assert: Both instances must pick the exact same shard index
            Assert.True(receivedIndex1 >= 0);
            Assert.Equal(receivedIndex1, receivedIndex2);
        }

        [Fact]
        public async Task EnqueueAsync_HandlesUnicodeAndSpecialCharacters_WithoutFailing() {
            // Arrange
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            string[] complexKeys = [
                "müşteri_türkçe_karakterler_öçşğü",
                "emoji-tenant-🚀-🔥-🌍",
                "cjk-part-ユーザー-123",
                "symbols_!@#$%^&*()_+{}|:\"<>?"
            ];

            // Act & Assert: All keys must hash and route without throwing
            foreach(string key in complexKeys) {
                WebhookDeliveryJob job = WebhookTestFactory.CreateJob(
                    WebhookJobId.NewJobId(),
                    WebhookTestFactory.CreateEndpointId(),
                    new WebhookPartitionKey(key),
                    "unicode.test",
                    WebhookTestFactory.CreateEvent());

                await sharded.EnqueueAsync(job, TestContext.Current.CancellationToken);
            }

            int totalEnqueued = shards.Sum(s => {
                int count = 0;
                while(s.Reader.TryRead(out _)) count++;
                return count;
            });

            Assert.Equal(complexKeys.Length, totalEnqueued);
        }

        [Fact]
        public async Task EnqueueAsync_HandlesExtremelyLongPartitionKeys_WithoutCrashing() {
            using InMemoryWebhookTransport shard = new();
            using ShardedWebhookTransport sharded = new([shard]);

            string longKey = new('x', 10_000);
            WebhookDeliveryJob job = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(longKey),
                "long.key.test",
                WebhookTestFactory.CreateEvent());

            await sharded.EnqueueAsync(job, TestContext.Current.CancellationToken);

            Assert.True(shard.Reader.TryRead(out WebhookDeliveryJob? dequeued));
            Assert.Same(job, dequeued);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 3. BATCH ROUTING & BULK DISPATCHING (NEW)
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheBatchJobRoutingBehavior {
        [Fact]
        public async Task EnqueueBatchAsync_RoutesEachJobToCorrectShard_BasedOnPartitionKey() {
            // Arrange: 4 Shards (Power of Two)
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            WebhookDeliveryJob[] batchJobs = Enumerable.Range(1, 40).Select(i => {
                string partKey = $"tenant-batch-{i}";
                return WebhookTestFactory.CreateJob(
                    WebhookJobId.NewJobId(),
                    WebhookTestFactory.CreateEndpointId($"ep-{i}"),
                    new WebhookPartitionKey(partKey),
                    "order.created",
                    WebhookTestFactory.CreateEvent());
            }).ToArray();

            // Act: Dispatch 40 jobs in a single batch call
            await sharded.EnqueueBatchAsync(batchJobs, TestContext.Current.CancellationToken);

            // Assert: Verify each job landed in its mathematically expected shard
            int totalDequeued = 0;
            for(int i = 0; i < batchJobs.Length; i++) {
                WebhookDeliveryJob job = batchJobs[i];
                ulong hash = XxHash3.Compute(job.PartitionKey.AsSpan()).Value;
                int expectedShardIndex = (int)(hash & 3ul);

                InMemoryWebhookTransport targetShard = shards[expectedShardIndex];
                Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? dequeued));
                Assert.Equal(job.Id, dequeued.Id);
                Assert.Equal(job.PartitionKey, dequeued.PartitionKey);
                totalDequeued++;
            }

            Assert.Equal(40, totalDequeued);
        }

        [Fact]
        public async Task EnqueueBatchAsync_PreservesRelativeFifoOrder_ForIdenticalPartitionKeys() {
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            const string sharedPartitionKey = "shared-batch-aggregate-1";
            WebhookDeliveryJob[] sequentialJobs = Enumerable.Range(1, 10).Select(seq =>
                WebhookTestFactory.CreateJob(
                    WebhookJobId.NewJobId(),
                    WebhookTestFactory.CreateEndpointId("customer-1"),
                    new WebhookPartitionKey(sharedPartitionKey),
                    $"order.seq.{seq}",
                    WebhookTestFactory.CreateEvent())
            ).ToArray();

            // Act
            await sharded.EnqueueBatchAsync(sequentialJobs, TestContext.Current.CancellationToken);

            ulong hash = XxHash3.Compute(sharedPartitionKey.AsSpan()).Value;
            int expectedShardIndex = (int)(hash & 3ul);
            InMemoryWebhookTransport targetShard = shards[expectedShardIndex];

            // Assert: All 10 jobs must drain from the target shard in exact sequence (1..10)
            for(int expectedSeq = 1; expectedSeq <= 10; expectedSeq++) {
                Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? dequeued));
                Assert.Equal($"order.seq.{expectedSeq}", dequeued.EventType);
            }
        }

        [Fact]
        public async Task EnqueueBatchAsync_WhenBatchIsEmpty_CompletesImmediatelyWithoutWriting() {
            InMemoryWebhookTransport[] shards = [new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            // Act
            await sharded.EnqueueBatchAsync([], TestContext.Current.CancellationToken);

            // Assert: No shards should contain any jobs
            Assert.False(shards[0].Reader.TryRead(out _));
            Assert.False(shards[1].Reader.TryRead(out _));
        }

        [Fact]
        public async Task EnqueueBatchAsync_Throws_WhenBatchIsNull() {
            using InMemoryWebhookTransport shard = new();
            using ShardedWebhookTransport sharded = new([shard]);

            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                sharded.EnqueueBatchAsync(null!, TestContext.Current.CancellationToken));
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 4. DELAYED SCHEDULING & RETRIES
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheDelayedScheduling {
        [Fact]
        public async Task EnqueueAsync_WithDelay_ForwardsDelayToCorrectShard() {
            // Arrange
            InMemoryWebhookTransport[] shards = [new(), new(), new(), new()];
            using ShardedWebhookTransport sharded = new(shards);

            const string partitionKey = "delayed-retry-partition-key";
            WebhookDeliveryJob job = WebhookTestFactory.CreateJob(
                WebhookJobId.NewJobId(),
                WebhookTestFactory.CreateEndpointId(),
                new WebhookPartitionKey(partitionKey),
                "retry.event",
                WebhookTestFactory.CreateEvent());

            ulong hash = XxHash3.Compute(partitionKey.AsSpan()).Value;
            int expectedShardIndex = (int)(hash & 3ul);
            InMemoryWebhookTransport targetShard = shards[expectedShardIndex];

            // Act: Enqueue with 50ms delay
            await sharded.EnqueueAsync(job, TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);

            // Immediately: Job is buffered by scheduler, not yet readable
            Assert.False(targetShard.Reader.TryRead(out _));

            // Wait for timer flush
            await Task.Delay(100, TestContext.Current.CancellationToken);

            // Assert: Job flushes into the exact intended shard
            Assert.True(targetShard.Reader.TryRead(out WebhookDeliveryJob? dequeued));
            Assert.Same(job, dequeued);
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 5. HIGH CONCURRENCY & STRICT FIFO INVARIANT STRESS TEST
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheConcurrencyStressTests {
        [Fact]
        public async Task EnqueueAsync_UnderHighConcurrency_PreservesStrictFifoSequence_PerPartition() {
            // Arrange: 8 shards
            InMemoryWebhookTransport[] shards = Enumerable.Range(0, 8).Select(_ => new InMemoryWebhookTransport()).ToArray();
            using ShardedWebhookTransport sharded = new(shards);

            const int partitionCount = 10;
            const int eventsPerPartition = 50;

            // Act: Dispatch 500 events concurrently across 10 distinct partition keys
            Task[] tasks = Enumerable.Range(0, partitionCount).Select(partitionIndex => {
                string partitionKey = $"partition-{partitionIndex}";
                return Task.Run(async () => {
                    for(int sequenceId = 0; sequenceId < eventsPerPartition; sequenceId++) {
                        OrderCreatedWebhookEvent payload = WebhookTestFactory.CreateEvent();
                        WebhookDeliveryJob job = WebhookTestFactory.CreateJob(
                            WebhookJobId.NewJobId(),
                            WebhookTestFactory.CreateEndpointId($"ep-{partitionIndex}"),
                            new WebhookPartitionKey(partitionKey),
                            $"event.seq.{sequenceId}",
                            payload);

                        await sharded.EnqueueAsync(job, TestContext.Current.CancellationToken);
                    }
                }, TestContext.Current.CancellationToken);
            }).ToArray();

            await Task.WhenAll(tasks);

            // Assert: Drain each shard and verify sequence numbers are strictly incrementing (FIFO) per partition
            Dictionary<string, List<int>> observedSequencesPerPartition = [];

            for(int i = 0; i < shards.Length; i++) {
                while(shards[i].Reader.TryRead(out WebhookDeliveryJob? job)) {
                    Assert.NotNull(job);
                    string partKey = job.PartitionKey.Value;
                    int seqId = int.Parse(job.EventType.Replace("event.seq.", ""));

                    if(!observedSequencesPerPartition.TryGetValue(partKey, out List<int>? list)) {
                        list = [];
                        observedSequencesPerPartition[partKey] = list;
                    }
                    list.Add(seqId);
                }
            }

            Assert.Equal(partitionCount, observedSequencesPerPartition.Count);

            foreach((string partitionKey, List<int> sequences) in observedSequencesPerPartition) {
                Assert.Equal(eventsPerPartition, sequences.Count);
                for(int expectedSeq = 0; expectedSeq < eventsPerPartition; expectedSeq++) {
                    Assert.Equal(expectedSeq, sequences[expectedSeq]);
                }
            }
        }
    }

    // ────────────────────────────────────────────────────────────────────────
    // 6. GUARDS AND CANCELLATION
    // ────────────────────────────────────────────────────────────────────────

    public sealed class TheGuardsAndCancellation {
        [Fact]
        public async Task EnqueueAsync_Throws_WhenJobIsNull() {
            using InMemoryWebhookTransport shard = new();
            using ShardedWebhookTransport sharded = new([shard]);

            await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
                sharded.EnqueueAsync(null!, TestContext.Current.CancellationToken));
        }

        [Fact]
        public async Task EnqueueAsync_ThrowsOperationCanceledException_WhenCancelledOnBoundedFullShard() {
            InMemoryWebhookTransport boundedShard = new(capacity: 1);
            using ShardedWebhookTransport sharded = new([boundedShard]);

            WebhookDeliveryJob fillerJob = WebhookTestFactory.CreateJob();
            await sharded.EnqueueAsync(fillerJob, TestContext.Current.CancellationToken);

            using CancellationTokenSource cts = new();
            cts.Cancel();

            WebhookDeliveryJob blockedJob = WebhookTestFactory.CreateJob();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                sharded.EnqueueAsync(blockedJob, cts.Token));
        }
    }
}