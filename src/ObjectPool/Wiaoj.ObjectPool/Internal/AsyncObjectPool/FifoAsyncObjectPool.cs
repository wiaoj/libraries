using System.Collections.Concurrent;
using Wiaoj.Concurrency;
using Wiaoj.ObjectPool.Abstractions;
using Wiaoj.ObjectPool.Configuration;
using Wiaoj.ObjectPool.Core;

namespace Wiaoj.ObjectPool.Internal.AsyncObjectPool;

internal sealed class FifoAsyncObjectPool<T> : IAsyncObjectPool<T>, IObjectPool<T> where T : class {
    private readonly ConcurrentQueue<T> _queue = new();
    private readonly IAsyncPoolPolicy<T> _policy;
    private readonly ObjectPoolOptions _options;
    private readonly int _maxCapacity;

    private int _count;

    public FifoAsyncObjectPool(IAsyncPoolPolicy<T> policy, ObjectPoolOptions options) {
        this._policy = policy;
        this._options = options;
        this._maxCapacity = options.MaximumRetained;
    }

    public async ValueTask<T> GetAsync(CancellationToken cancellationToken = default) {
        T instance;
        if (this._queue.TryDequeue(out T? item)) {
            Atomic.Decrement(ref this._count);
            instance = item;
        }
        else {
            instance = await this._policy.CreateAsync(cancellationToken).ConfigureAwait(false);
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
        // 1. Leak Detection Kapatma
        if (this._options.LeakDetectionEnabled) {
            LeakDetector.Untrack(obj);
        }

        // 2. Validation Kontrolü
        if (this._options.OnReturnValidation is not null) {
            try { this._options.OnReturnValidation(obj); }
            catch (Exception ex) {
                // Validation patlarsa objeyi havuza alma, direkt at.
                // Loglama yapılabilir.
                DisposeItem(obj);
                // Eğer sayacı artırdıysak (aşağıda) geri almamız gerekir ama şu an daha logic'e girmedik.
                // Burada logic biraz karışık, en iyisi validation'ı logic öncesi yapmak.
                throw new InvalidOperationException("Validation failed on return.", ex);
            }
        }
#endif

        int currentCount = Atomic.Read(ref this._count);
        if (currentCount >= this._maxCapacity) {
            DisposeItem(obj);
            return;
        }

        int newCount = Atomic.Increment(ref this._count);
        if (newCount > this._maxCapacity) {
            Atomic.Decrement(ref this._count);
            DisposeItem(obj);
            return;
        }

        ValueTask<bool> resetTask = this._policy.TryResetAsync(obj);
        if (resetTask.IsCompletedSuccessfully) {
            if (resetTask.Result) {
                this._queue.Enqueue(obj);
            }
            else { Atomic.Decrement(ref this._count); DisposeItem(obj); }
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
        else { Atomic.Decrement(ref this._count); DisposeItem(obj); }
    }

    public async ValueTask<PooledObject<T>> LeaseAsync(CancellationToken cancellationToken = default) {
        T item = await GetAsync(cancellationToken).ConfigureAwait(false);
        return new PooledObject<T>(item, this);
    }

    public PoolStats GetStats() {
        return new PoolStats(this._queue.Count, this._maxCapacity);
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