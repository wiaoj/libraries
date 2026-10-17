using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Wiaoj.Webhooks.Tests.Unit.TestData;
using Wiaoj.Webhooks.Transports.InMemory;

namespace Wiaoj.Webhooks.Tests.Unit.Transport;

[Trait("Category", "Unit")]
[Trait("Feature", "Transport")]
[Trait("Component", "ShardedWebhookTransport")]
public sealed class ShardedWebhookTransportTests {
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>A shard that remembers what it was handed, in order.</summary>
    private sealed class RecordingShard : IWebhookTransport, IDisposable {
        public List<(WebhookDeliveryJob Job, TimeSpan? Delay)> Received { get; } = [];
        public int Disposed { get; private set; }

        public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) => this.EnqueueAsync(job, null, cancellationToken);
        public Task EnqueueAsync(WebhookDeliveryJob job) => this.EnqueueAsync(job, null, CancellationToken.None);
        public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => this.EnqueueAsync(job, delay, CancellationToken.None);

        public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
            this.Received.Add((job, delay));
            return Task.CompletedTask;
        }

        public async Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) {
            foreach(WebhookDeliveryJob job in jobs) {
                await this.EnqueueAsync(job, null, cancellationToken);
            }
        }

        public void Dispose() => this.Disposed++;
    }

    /// <summary>A shard that cannot be disposed, to prove disposal skips such shards.</summary>
    private sealed class PlainShard : IWebhookTransport {
        public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EnqueueAsync(WebhookDeliveryJob job) => Task.CompletedTask;
        public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) => Task.CompletedTask;
        public Task EnqueueBatchAsync(IReadOnlyList<WebhookDeliveryJob> jobs, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static RecordingShard[] Shards(int count) => [.. Enumerable.Range(0, count).Select(_ => new RecordingShard())];

    private static ShardedWebhookTransport Create(RecordingShard[] shards) => new([.. shards]);

    private static WebhookDeliveryJob JobFor(string partitionKey) {
        WebhookEndpointId endpoint = WebhookTestFactory.CreateEndpointId(partitionKey);
        return WebhookTestFactory.CreateJob(
            WebhookJobId.NewJobId(), endpoint, WebhookTestFactory.CreatePartitionKey(partitionKey),
            WebhookTestConstants.EventTypeValue, WebhookTestFactory.CreateEvent());
    }

    private static int ShardOf(RecordingShard[] shards, WebhookDeliveryJob job) =>
        Array.FindIndex(shards, shard => shard.Received.Any(entry => entry.Job == job));

    public sealed class TheConstruction {
        [Fact]
        public void NoShards_IsRefused() {
            Assert.ThrowsAny<ArgumentException>(() => new ShardedWebhookTransport([]));
            Assert.ThrowsAny<ArgumentNullException>(() => new ShardedWebhookTransport(null!));
        }

        [Fact]
        public void ShardCount_IsTheNumberOfShards() {
            Assert.Equal(5, Create(Shards(5)).ShardCount);
        }
    }

    public sealed class TheRouting {
        [Theory]
        [InlineData(1)]
        [InlineData(3)]   // not a power of two: modulo path
        [InlineData(4)]   // power of two: mask path
        [InlineData(8)]
        public async Task ASamePartitionKeyAlwaysGoesToTheSameShard(int shardCount) {
            RecordingShard[] shards = Shards(shardCount);
            ShardedWebhookTransport transport = Create(shards);

            WebhookDeliveryJob[] jobs = [.. Enumerable.Range(0, 20).Select(_ => JobFor("tenant-42"))];
            foreach(WebhookDeliveryJob job in jobs) {
                await transport.EnqueueAsync(job, Ct);
            }

            Assert.Single(shards.Where(shard => shard.Received.Count > 0));
            Assert.Equal(jobs, shards.Single(shard => shard.Received.Count > 0).Received.Select(entry => entry.Job));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public async Task DifferentKeysSpreadAcrossEveryShard(int shardCount) {
            RecordingShard[] shards = Shards(shardCount);
            ShardedWebhookTransport transport = Create(shards);

            for(int i = 0; i < 1000; i++) {
                await transport.EnqueueAsync(JobFor($"tenant-{i}"), Ct);
            }

            int expected = 1000 / shardCount;
            foreach(RecordingShard shard in shards) {
                // Hashing is deterministic, so this is not a statistical test: it fails only if the spread is badly skewed.
                Assert.InRange(shard.Received.Count, expected * 7 / 10, expected * 13 / 10);
            }
        }

        [Fact]
        public async Task TheSameKeyKeepsItsOrder_WhileOtherKeysInterleave() {
            RecordingShard[] shards = Shards(4);
            ShardedWebhookTransport transport = Create(shards);
            List<WebhookDeliveryJob> mine = [];

            for(int i = 0; i < 50; i++) {
                WebhookDeliveryJob job = JobFor("mine");
                mine.Add(job);
                await transport.EnqueueAsync(job, Ct);
                await transport.EnqueueAsync(JobFor($"other-{i}"), Ct);
            }

            RecordingShard owner = shards[ShardOf(shards, mine[0])];
            Assert.Equal(mine, owner.Received.Select(entry => entry.Job).Where(job => mine.Contains(job)));
        }

        [Fact]
        public async Task TheDelayIsPassedToTheShard_ForEveryOverload() {
            RecordingShard[] shards = Shards(1);
            ShardedWebhookTransport transport = Create(shards);
            TimeSpan delay = TimeSpan.FromSeconds(7);

            await transport.EnqueueAsync(JobFor("k"), delay);
            await transport.EnqueueAsync(JobFor("k"), delay, Ct);
            await transport.EnqueueAsync(JobFor("k"), Ct);
            await transport.EnqueueAsync(JobFor("k"));

            Assert.Equal([delay, delay, null, null], shards[0].Received.Select(entry => entry.Delay));
        }

        [Fact]
        public async Task ANullJob_IsRefused() {
            ShardedWebhookTransport transport = Create(Shards(2));

            await Assert.ThrowsAnyAsync<ArgumentNullException>(() => transport.EnqueueAsync(null!, Ct));
        }
    }

    public sealed class TheBatch {
        [Fact]
        public async Task EachJobGoesToItsOwnShard_InTheOrderGiven() {
            RecordingShard[] shards = Shards(4);
            ShardedWebhookTransport transport = Create(shards);
            WebhookDeliveryJob[] batch = [.. Enumerable.Range(0, 40).Select(i => JobFor($"tenant-{i % 8}"))];

            await transport.EnqueueBatchAsync(batch, Ct);

            Assert.Equal(40, shards.Sum(shard => shard.Received.Count));
            foreach(IGrouping<string, WebhookDeliveryJob> group in batch.GroupBy(job => job.PartitionKey.Value)) {
                RecordingShard owner = shards[ShardOf(shards, group.First())];
                Assert.Equal(group, owner.Received.Select(entry => entry.Job).Where(job => group.Contains(job)));
            }
        }

        [Fact]
        public async Task AnEmptyBatch_DoesNothing() {
            RecordingShard[] shards = Shards(2);

            await Create(shards).EnqueueBatchAsync([], Ct);

            Assert.All(shards, shard => Assert.Empty(shard.Received));
        }

        [Fact]
        public async Task ACancelledBatch_StopsBeforeEnqueuingAnything() {
            RecordingShard[] shards = Shards(2);
            using CancellationTokenSource cts = new();
            await cts.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Create(shards).EnqueueBatchAsync([JobFor("a"), JobFor("b")], cts.Token));

            Assert.All(shards, shard => Assert.Empty(shard.Received));
        }
    }

    public sealed class TheShardAccessor {
        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void GetShard_OutOfRange_IsRefused(int index) {
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(Shards(4)).GetShard(index));
        }

        [Fact]
        public void GetShard_ReturnsTheShardAtThatIndex() {
            RecordingShard[] shards = Shards(3);
            ShardedWebhookTransport transport = Create(shards);

            for(int i = 0; i < shards.Length; i++) {
                Assert.Same(shards[i], transport.GetShard(i));
            }
        }
    }

    public sealed class TheDisposal {
        [Fact]
        public void EveryDisposableShardIsDisposed_AndOthersAreSkipped() {
            RecordingShard first = new();
            RecordingShard second = new();
            ShardedWebhookTransport transport = new([first, new PlainShard(), second]);

            transport.Dispose();

            Assert.Equal(1, first.Disposed);
            Assert.Equal(1, second.Disposed);
        }
    }

    public sealed class TheRegistration {
        private static ServiceProvider Build(Action<IWebhookBuilder> configure) {
            ServiceCollection services = new();
            services.AddLogging();
            IWebhookBuilder builder = services.AddWiaojWebhooks();
            configure(builder);
            return services.BuildServiceProvider();
        }

        [Fact]
        public void DefaultsToEightInMemoryShards_AndRegistersTheConsumer() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseShardedInMemoryTransport());

            ShardedWebhookTransport transport = Assert.IsType<ShardedWebhookTransport>(provider.GetRequiredService<IWebhookTransport>());
            Assert.Equal(8, transport.ShardCount);
            Assert.All(Enumerable.Range(0, 8), index => Assert.IsType<InMemoryWebhookTransport>(transport.GetShard(index)));
            Assert.Contains(provider.GetServices<IHostedService>(), service => service is InMemoryWebhookConsumer);
        }

        [Fact]
        public void TheRequestedShardCountIsUsed() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseShardedInMemoryTransport(shardCount: 3));

            Assert.Equal(3, Assert.IsType<ShardedWebhookTransport>(provider.GetRequiredService<IWebhookTransport>()).ShardCount);
        }

        [Fact]
        public void ItReplacesATransportRegisteredEarlier() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseInMemoryTransport().UseShardedInMemoryTransport(2));

            Assert.IsType<ShardedWebhookTransport>(provider.GetRequiredService<IWebhookTransport>());
            Assert.Single(provider.GetServices<IWebhookTransport>());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public void ANonPositiveShardCount_IsRefused(int shardCount) {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.ThrowsAny<ArgumentException>(() => builder.UseShardedInMemoryTransport(shardCount));
        }

        [Fact]
        public void ANonPositiveCapacity_IsRefused() {
            ServiceCollection services = new();
            IWebhookBuilder builder = services.AddWiaojWebhooks();

            Assert.ThrowsAny<ArgumentException>(() => builder.UseShardedInMemoryTransport(2, capacityPerShard: 0));
        }

        [Fact]
        public async Task CapacityIsPerShard_AFullShardWaitsWhileAnotherStillAccepts() {
            using ServiceProvider provider = Build(webhooks => webhooks.UseShardedInMemoryTransport(shardCount: 2, capacityPerShard: 1));
            ShardedWebhookTransport transport = Assert.IsType<ShardedWebhookTransport>(provider.GetRequiredService<IWebhookTransport>());

            // Hashing is deterministic: sort candidate keys by the shard a recording router sends them to.
            RecordingShard[] probe = Shards(2);
            ShardedWebhookTransport router = Create(probe);
            List<string>[] keysByShard = [[], []];
            for(int i = 0; keysByShard[0].Count < 2 || keysByShard[1].Count < 1; i++) {
                string key = $"key-{i}";
                WebhookDeliveryJob job = JobFor(key);
                await router.EnqueueAsync(job, Ct);
                keysByShard[ShardOf(probe, job)].Add(key);
            }

            await transport.EnqueueAsync(JobFor(keysByShard[0][0]), Ct);                 // fills shard 0
            Task blocked = transport.EnqueueAsync(JobFor(keysByShard[0][1]), Ct);        // shard 0 has no reader: waits
            Task other = transport.EnqueueAsync(JobFor(keysByShard[1][0]), Ct);          // shard 1 is empty

            Assert.False(blocked.IsCompleted);
            await other;

            using CancellationTokenSource cts = new();
            await cts.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                transport.EnqueueAsync(JobFor(keysByShard[0][1]), cts.Token));
        }
    }
}
