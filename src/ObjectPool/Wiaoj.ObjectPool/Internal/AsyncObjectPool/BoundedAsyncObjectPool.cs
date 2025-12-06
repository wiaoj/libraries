using System.Collections.Concurrent;
using Wiaoj.ObjectPool.Abstractions;
using Wiaoj.ObjectPool.Configuration;
using Wiaoj.ObjectPool.Core;

namespace Wiaoj.ObjectPool.Internal.AsyncObjectPool;

internal sealed class BoundedAsyncObjectPool<T> : IAsyncObjectPool<T>, IObjectPool<T> where T : class {
    private readonly ConcurrentQueue<T> _queue = new();
    private readonly IAsyncPoolPolicy<T> _policy;
    private readonly SemaphoreSlim _semaphore;
    private readonly ObjectPoolOptions _options; // Options eklendi
    private readonly int _maxCapacity;

    public BoundedAsyncObjectPool(IAsyncPoolPolicy<T> policy, ObjectPoolOptions options) {
        this._policy = policy;
        this._options = options; // Sakla
        this._maxCapacity = options.MaximumRetained;
        this._semaphore = new SemaphoreSlim(options.MaximumRetained, options.MaximumRetained);
    }

    public async ValueTask<T> GetAsync(CancellationToken cancellationToken = default) {
        await this._semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        T instance;
        try {
            if (this._queue.TryDequeue(out T? item)) {
                instance = item;
            }
            else {
                instance = await this._policy.CreateAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch {
            this._semaphore.Release();
            throw;
        }

#if DEBUG
        if (this._options.LeakDetectionEnabled) {
            LeakDetector.Track(instance);
        }
#endif
        return instance;
    }

    public void Return(T obj) {
#if DEBUG
        if (this._options.LeakDetectionEnabled) {
            LeakDetector.Untrack(obj);
        }

        if (this._options.OnReturnValidation is not null) {
            try { this._options.OnReturnValidation(obj); }
            catch (Exception ex) {                                                    
                DisposeItem(obj);
                this._semaphore.Release();
                throw new InvalidOperationException("Validation failed on return.", ex);
            }
        }
#endif

        ValueTask<bool> resetTask = this._policy.TryResetAsync(obj);
        if (resetTask.IsCompletedSuccessfully) {
            if (resetTask.Result) {
                this._queue.Enqueue(obj);
            }
            else {
                DisposeItem(obj);
            }

            this._semaphore.Release();
        }
        else {
            _ = ReturnAsyncSlow(resetTask, obj);
        }
    }

    private async Task ReturnAsyncSlow(ValueTask<bool> task, T obj) {
        bool success = false;
        try { success = await task.ConfigureAwait(false); }
        catch { }

        if (success) {
            this._queue.Enqueue(obj);
        }
        else {
            DisposeItem(obj);
        }

        this._semaphore.Release();
    }

    public async ValueTask<PooledObject<T>> LeaseAsync(CancellationToken cancellationToken = default) {
        T item = await GetAsync(cancellationToken).ConfigureAwait(false);
        return new PooledObject<T>(item, this);
    }

    public PoolStats GetStats() {
        return new PoolStats(this._semaphore.CurrentCount, this._maxCapacity);
    }

    private static void DisposeItem(T item) {
        if (item is IDisposable d) {
            d.Dispose();
        }
        else if (item is IAsyncDisposable ad) {
            _ = ad.DisposeAsync();
        }
    }

    T IObjectPool<T>.Get() {
        throw new NotSupportedException("Use GetAsync");
    }

    PooledObject<T> IObjectPool<T>.Lease() {
        throw new NotSupportedException("Use LeaseAsync");
    }

    void IObjectPool<T>.Return(T obj) {
        Return(obj);
    }
}