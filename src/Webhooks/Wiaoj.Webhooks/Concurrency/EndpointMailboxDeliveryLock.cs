using System.Collections.Concurrent;

namespace Wiaoj.Webhooks.Concurrency;

/// <summary>
/// High-performance, zero-collision endpoint delivery serializer ensuring complete cross-tenant isolation
/// using an atomic reference-counted keyed synchronization mechanism.
/// </summary>
/// <remarks>
/// <para>
/// Unlike striped hashing locks, this implementation dynamically allocates a lightweight synchronization node
/// per active <see cref="WebhookEndpointId"/>. Distinct endpoints execute in parallel with <b>zero cross-blocking</b>,
/// even when processing millions of unique destinations.
/// </para>
/// <para>
/// <b>Safe Memory Reclamation:</b> Employs an internal reference counter (<c>RefCount</c>) to safely dispose and
/// remove idle synchronization nodes from memory when no concurrent requests are queued, preventing memory leaks.
/// </para>
/// </remarks>
public sealed class EndpointMailboxDeliveryLock : IWebhookDeliveryLock {
    private readonly ConcurrentDictionary<string, LockNode> _nodes = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public ValueTask<IDisposable> AcquireLockAsync(WebhookEndpointId endpointId, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(endpointId.Value);
        return AcquireLockAsync(endpointId.Value, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<IDisposable> AcquireLockAsync(string partitionKey, CancellationToken cancellationToken = default) {
        Preca.ThrowIfNullOrWhiteSpace(partitionKey);

        LockNode node;

        while(true) {
            node = this._nodes.GetOrAdd(partitionKey, static _ => new LockNode());

            lock(node) {
                if(!node.IsDisposed) {
                    node.RefCount++;
                    break;
                }
            }
        }

        try {
            await node.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(this._nodes, partitionKey, node);
        }
        catch {
            lock(node) {
                node.RefCount--;
                if(node.RefCount == 0) {
                    node.IsDisposed = true;
                    this._nodes.TryRemove(partitionKey, out _);
                    node.Semaphore.Dispose();
                }
            }
            throw;
        }
    }

    private sealed class LockNode {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
        public bool IsDisposed;
    }

    private sealed class Releaser(
        ConcurrentDictionary<string, LockNode> nodes,
        string partitionKey,
        LockNode node) : IDisposable {

        private bool _isDisposed;

        public void Dispose() {
            if(this._isDisposed) return;
            this._isDisposed = true;

            node.Semaphore.Release();

            lock(node) {
                node.RefCount--;
                if(node.RefCount == 0) {
                    node.IsDisposed = true;
                    nodes.TryRemove(partitionKey, out _);
                    node.Semaphore.Dispose();
                }
            }
        }
    }
}