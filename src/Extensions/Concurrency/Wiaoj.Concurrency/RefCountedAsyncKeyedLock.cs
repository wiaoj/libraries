using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wiaoj.Concurrency;

/// <summary>
/// A generic, reference-counted, per-key asynchronous lock. Distinct keys execute with
/// zero cross-blocking; idle synchronization nodes are automatically reclaimed when their
/// reference count reaches zero, preventing unbounded memory growth.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="StripedLock{TKey}"/>, which partitions keys across a fixed set of locks
/// (risking false contention between unrelated keys that hash to the same stripe), this type
/// dynamically allocates a dedicated <see cref="SemaphoreSlim"/> per active key. Prefer
/// <see cref="StripedLock{TKey}"/> when the key space is large/unbounded and occasional false
/// contention is acceptable; prefer this type when true per-key isolation matters more than
/// the allocation overhead of dynamic node creation.
/// </para>
/// </remarks>
/// <typeparam name="TKey">The type of the key to lock on. Must be non-null.</typeparam>
[DebuggerDisplay("ActiveKeys = {_nodes.Count}")]
public sealed class RefCountedAsyncKeyedLock<TKey> where TKey : notnull {
    private readonly ConcurrentDictionary<TKey, LockNode> _nodes;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefCountedAsyncKeyedLock{TKey}"/> class.
    /// </summary>
    /// <param name="comparer">An optional equality comparer for <typeparamref name="TKey"/>.</param>
    public RefCountedAsyncKeyedLock(IEqualityComparer<TKey>? comparer = null) {
        this._nodes = new ConcurrentDictionary<TKey, LockNode>(comparer ?? EqualityComparer<TKey>.Default);
    }

    /// <summary>
    /// Asynchronously acquires the lock associated with the specified key.
    /// </summary>
    /// <param name="key">The key to lock on.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests while waiting.</param>
    /// <returns>A disposable scope that releases the lock when disposed.</returns>
    public async ValueTask<IDisposable> AcquireAsync(TKey key, CancellationToken cancellationToken = default) {
        LockNode node;

        while(true) {
            node = this._nodes.GetOrAdd(key, static _ => new LockNode());

            lock(node) {
                if(!node.IsDisposed) {
                    node.RefCount++;
                    break;
                }
            }
            // Concurrent release race disposed the node between GetOrAdd and the lock; retry with a fresh one.
        }

        try {
            await node.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Releaser(this, key, node);
        }
        catch {
            ReleaseNode(key, node);
            throw;
        }
    }

    private void ReleaseNode(TKey key, LockNode node) {
        lock(node) {
            node.RefCount--;
            if(node.RefCount == 0) {
                node.IsDisposed = true;
                this._nodes.TryRemove(key, out _);
                node.Semaphore.Dispose();
            }
        }
    }

    private sealed class LockNode {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
        public bool IsDisposed;
    }

    private sealed class Releaser(RefCountedAsyncKeyedLock<TKey> owner, TKey key, LockNode node) : IDisposable {
        private bool _isDisposed;

        public void Dispose() {
            if(this._isDisposed) return;
            this._isDisposed = true;

            node.Semaphore.Release();
            owner.ReleaseNode(key, node);
        }
    }
}