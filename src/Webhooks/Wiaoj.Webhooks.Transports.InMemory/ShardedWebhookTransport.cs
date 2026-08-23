using System.Numerics;
using System.Runtime.CompilerServices;
using Wiaoj.Primitives.Hashing;

namespace Wiaoj.Webhooks.Transports.InMemory;

/// <summary>
/// High-throughput partition router that deterministically shards outbound webhook deliveries across an array of
/// underlying <see cref="IWebhookTransport"/> instances using process-independent deterministic hashing (<see cref="XxHash3"/>)
/// and optimized single-cycle bitwise masking.
/// </summary>
/// <remarks>
/// <para>
/// <b>Deterministic Ordering:</b> Deliveries with identical <see cref="WebhookDeliveryJob.PartitionKey"/> are
/// guaranteed to route to the exact same transport shard across process restarts and cluster nodes.
/// </para>
/// <para>
/// <b>Zero Lock Contention:</b> Shards execute independently, enabling maximum CPU saturation across parallel worker loops.
/// </para>
/// </remarks>
public sealed class ShardedWebhookTransport : IWebhookTransport, IDisposable {
    private readonly IWebhookTransport[] _shards;
    private readonly ulong _mask;
    private readonly bool _isPowerOfTwo;

    /// <summary>
    /// Gets the total number of underlying transport shards.
    /// </summary>
    public int ShardCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShardedWebhookTransport"/> class wrapping the specified transport shards.
    /// </summary>
    /// <param name="shards">The array of underlying transport instances. Cannot be null or empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="shards"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="shards"/> is empty.</exception>
    public ShardedWebhookTransport(IWebhookTransport[] shards) {
        Preca.ThrowIfNull(shards);
        Preca.ThrowIfLessThan(shards.Length, 1);

        this._shards = shards;
        this.ShardCount = shards.Length;
        this._isPowerOfTwo = BitOperations.IsPow2(this.ShardCount);
        this._mask = (ulong)(this.ShardCount - 1);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, CancellationToken cancellationToken) {
        return EnqueueAsync(job, null, cancellationToken);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job) {
        return EnqueueAsync(job, null, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay) {
        return EnqueueAsync(job, delay, CancellationToken.None);
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(WebhookDeliveryJob job, TimeSpan? delay, CancellationToken cancellationToken) {
        Preca.ThrowIfNull(job);

        int shardIndex = GetShardIndex(job.PartitionKey.Value.AsSpan());
        return this._shards[shardIndex].EnqueueAsync(job, delay, cancellationToken);
    }

    /// <summary>
    /// Retrieves a specific underlying transport shard by its zero-based index.
    /// </summary>
    /// <param name="index">The zero-based shard index.</param>
    /// <returns>The underlying <see cref="IWebhookTransport"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="index"/> is out of valid bounds.</exception>
    public IWebhookTransport GetShard(int index) {
        if(index < 0 || index >= this.ShardCount) {
            throw new ArgumentOutOfRangeException(nameof(index), index, $"Shard index must be between 0 and {this.ShardCount - 1}.");
        }
        return this._shards[index];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetShardIndex(ReadOnlySpan<char> partitionKey) {
        ulong hash = XxHash3.Compute(partitionKey).Value;
        return this._isPowerOfTwo
            ? (int)(hash & this._mask)
            : (int)(hash % (ulong)this.ShardCount);
    }

    /// <summary>
    /// Disposes all underlying transport shards that implement <see cref="IDisposable"/>.
    /// </summary>
    public void Dispose() {
        for(int i = 0; i < this._shards.Length; i++) {
            if(this._shards[i] is IDisposable disposable) {
                disposable.Dispose();
            }
        }
    }
}