using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Webhooks.Concurrency;

/// <summary>
/// High-performance in-memory implementation of <see cref="IWebhookDeliveryLock"/> backed by a fixed array
/// of power-of-two non-blocking asynchronous stripes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Zero Heap Allocation on Lookup:</b> Uses bitwise masking (<c>hash &amp; (stripes - 1)</c>) to compute
/// the lock index in a single CPU instruction without division or modulo overhead.
/// </para>
/// <para>
/// <b>Fixed Memory Footprint:</b> Allocates all stripes at startup. Ideal for high-throughput single-instance
/// scenarios with bounded memory requirements.
/// </para>
/// </remarks>
public sealed class StripedWebhookDeliveryLock : IWebhookDeliveryLock, IDisposable {
    private readonly SemaphoreSlim[] _stripes;
    private readonly int _mask;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripedWebhookDeliveryLock"/> class with the default 4096 stripes.
    /// </summary>
    public StripedWebhookDeliveryLock() : this(4096) {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StripedWebhookDeliveryLock"/> class with the specified number of stripes.
    /// </summary>
    /// <param name="stripeCount">The number of lock stripes. Must be a positive power of two (e.g. 256, 1024, 4096).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="stripeCount"/> is less than 1 or not a power of two.</exception>
    public StripedWebhookDeliveryLock(int stripeCount) {
        Preca.ThrowIfLessThan(stripeCount, 1);
        if(!BitOperations.IsPow2(stripeCount)) {
            throw new ArgumentOutOfRangeException(nameof(stripeCount), stripeCount, "Stripe count must be a power of two.");
        }

        this._mask = stripeCount - 1;
        this._stripes = new SemaphoreSlim[stripeCount];
        for(int i = 0; i < stripeCount; i++) {
            this._stripes[i] = new SemaphoreSlim(1, 1);
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<IDisposable> AcquireLockAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(endpointId.Value);

        int hash = StringComparer.Ordinal.GetHashCode(endpointId.Value);
        int index = hash & this._mask;
        SemaphoreSlim semaphore = this._stripes[index];

        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public async ValueTask<IDisposable> AcquireLockAsync(string partitionKey, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(partitionKey);

        int hash = StringComparer.Ordinal.GetHashCode(partitionKey);
        int index = hash & this._mask;
        SemaphoreSlim semaphore = this._stripes[index];

        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Releaser(semaphore);
    }

    /// <summary>
    /// Disposes all internal synchronization primitives.
    /// </summary>
    public void Dispose() {
        for(int i = 0; i < this._stripes.Length; i++) {
            this._stripes[i].Dispose();
        }
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable {
        private bool _disposed;

        public void Dispose() {
            if(this._disposed) return;
            this._disposed = true;
            semaphore.Release();
        }
    }
}