using System.Collections.Concurrent;
using System.Diagnostics;

namespace Wiaoj.ObjectPool.Internal;

internal sealed class DefaultAsyncObjectPool<T>(IAsyncPoolPolicy<T> policy, ObjectPoolOptions options) : IAsyncObjectPool<T> where T : class {
    private readonly ConcurrentQueue<T> _items = new();
    private readonly int _maximumRetained = options.MaximumRetained;

    public async ValueTask<T> GetAsync(CancellationToken cancellationToken = default) {
        if (this._items.TryDequeue(out T? item)) {
#if DEBUG
            if (options.LeakDetectionEnabled)
                LeakDetector.Track(item);
#endif
            return item;
        }

        // Pool is empty, create a new one asynchronously.
        item = await policy.CreateAsync(cancellationToken).ConfigureAwait(false);

#if DEBUG
        if (options.LeakDetectionEnabled)
            LeakDetector.Track(item);
#endif
        return item;
    }

    public async ValueTask<PooledObject<T>> LeaseAsync(CancellationToken cancellationToken = default) {
        T instance = await GetAsync(cancellationToken).ConfigureAwait(false);
        // Note: We need a way to pass _options to the PooledObject context.
        // Let's assume PooledObject can take options or a reference to the pool itself.
        // For now, let's adapt it to use a synchronous return path.
        // The lease object needs a reference to the pool to return the item.
        // Since Return() is sync, the existing PooledObject can work, but it expects an IObjectPool<T>.
        // We'll need a small adapter or for PooledObject to be more flexible.
        // Easiest solution: Create an adapter that wraps this async pool.
        return new PooledObject<T>(instance, new AsyncPoolAdapter(this));
    }

    public void Return(T @object) {
        // Don't block the caller. Fire-and-forget the async reset logic.
        // If reset fails, the object is simply discarded. This is safe.
        _ = ReturnAsyncInternal(@object);
    }

    private async Task ReturnAsyncInternal(T @object) {
        bool canBeReused = this._items.Count < this._maximumRetained &&
                     await policy.TryResetAsync(@object).ConfigureAwait(false);

        if (canBeReused) {
            // Harika, yeniden kullanım için havuza ekle.
            this._items.Enqueue(@object);
        }
        else { 
            if (@object is IAsyncDisposable asyncDisposable) {
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            }
            else if (@object is IDisposable disposable) {
                disposable.Dispose();
            }
        }

#if DEBUG
        if (options.OnReturnValidation is not null) {
            try {
                options.OnReturnValidation(@object);
            }
            catch (Exception ex) {
                // We can't re-throw here without crashing the process. Log it instead.
                Debug.WriteLine($"[WIAOJ.OBJECTPOOL] Object validation failed on return: {ex.Message}");
            }
        } 

        if (options.LeakDetectionEnabled)
            LeakDetector.Untrack(@object);
#endif
    }

    // Simple adapter to make PooledObject<T> work with IAsyncObjectPool<T>
    // This allows reusing the PooledObject struct without changes.
    private sealed class AsyncPoolAdapter(IAsyncObjectPool<T> asyncPool) : IObjectPool<T> {
        public T Get() {
            throw new NotSupportedException("Use GetAsync on the async pool.");
        }

        public PooledObject<T> Lease() {
            throw new NotSupportedException("Use LeaseAsync on the async pool.");
        }

        public void Return(T @object) {
            asyncPool.Return(@object);
        }
    }
}