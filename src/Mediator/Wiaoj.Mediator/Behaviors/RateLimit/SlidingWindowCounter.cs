#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Wiaoj.Mediator.Behaviors; 
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Thread-safe sliding window rate limiter. Zero external dependencies.
/// Uses a circular timestamp queue under a <see cref="SemaphoreSlim"/> lock.
/// </summary>
internal sealed class SlidingWindowCounter {
    private readonly int _maxRequests;
    private readonly TimeSpan _window;
    private readonly Queue<long> _timestamps = new();  // UtcNow.Ticks
    private readonly SemaphoreSlim _lock = new(1, 1);

    internal SlidingWindowCounter(int maxRequests, TimeSpan window) {
        this._maxRequests = maxRequests;
        this._window = window;
    }

    /// <summary>
    /// Attempts to acquire a slot. Returns <c>false</c> immediately if the limit is exceeded.
    /// </summary>
    public async ValueTask<bool> TryAcquireAsync(CancellationToken ct = default) {
        await this._lock.WaitAsync(ct).ConfigureAwait(false);
        try {
            long now = DateTime.UtcNow.Ticks;
            long cutoff = now - this._window.Ticks;

            // Evict expired timestamps
            while(this._timestamps.Count > 0 && this._timestamps.Peek() < cutoff)
                this._timestamps.Dequeue();

            if(this._timestamps.Count >= this._maxRequests)
                return false;

            this._timestamps.Enqueue(now);
            return true;
        }
        finally {
            this._lock.Release();
        }
    }
}