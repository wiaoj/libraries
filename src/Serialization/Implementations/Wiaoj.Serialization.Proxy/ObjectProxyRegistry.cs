using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Serialization.Proxy;

/// <summary>
/// Yüksek performanslı, sharded (bölümlenmiş) nesne kayıt defteri.
/// </summary>
public sealed class ObjectProxyRegistry : IDisposable {

    // Record yerine Mutable Class kullanıyoruz. 
    // Allocation'ı ve Dictionary'nin lock mekanizmasını tamamen bypass eder.
    private sealed class LeaseEntry {
        public readonly object Value;
        public long ExpiryMs;

        public LeaseEntry(object value, long expiryMs) {
            this.Value = value;
            this.ExpiryMs = expiryMs;
        }
    }

    private readonly uint _shardCount = BitOperations.RoundUpToPowerOf2((uint)Environment.ProcessorCount);
    private readonly uint _shardMask;
    private long _nextId = 0;
    private readonly ConcurrentDictionary<long, LeaseEntry>[] _shards;
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer;

    private long _leaseDurationMs = 10000; // Default 10 sn

    public TimeSpan LeaseDuration {
        get => TimeSpan.FromMilliseconds(this._leaseDurationMs);
        set => this._leaseDurationMs = (long)value.TotalMilliseconds;
    }

    public ObjectProxyRegistry(TimeSpan? cleanupInterval = null) {
        this._shardMask = this._shardCount - 1;
        this._shards = new ConcurrentDictionary<long, LeaseEntry>[this._shardCount];

        // Büyüme (resize) maliyetini baştan önlemek için kapasiteler ayarlandı
        for(int i = 0; i < (int)this._shardCount; i++) {
            this._shards[i] = new ConcurrentDictionary<long, LeaseEntry>(Environment.ProcessorCount, 1024);
        }

        this._timer = new PeriodicTimer(cleanupInterval ?? TimeSpan.FromSeconds(5));
        _ = StartCleanupTaskAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Register(object value) {
        long id = Interlocked.Increment(ref this._nextId); 
        long expiry = Environment.TickCount64 + this._leaseDurationMs;

        int shardIndex = (int)(id & this._shardMask);
        this._shards[shardIndex].TryAdd(id, new LeaseEntry(value, expiry));
        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? Get(long id) {
        int shardIndex = (int)(id & this._shardMask);
        if(this._shards[shardIndex].TryGetValue(id, out LeaseEntry? entry)) { 
            if(Volatile.Read(ref entry.ExpiryMs) < Environment.TickCount64) {
                return null;
            }

            Volatile.Write(ref entry.ExpiryMs, Environment.TickCount64 + this._leaseDurationMs);
            return entry.Value;
        }
        return null;
    }

    private async Task StartCleanupTaskAsync() {
        try {
            while(await this._timer.WaitForNextTickAsync(this._cts.Token)) {
                long now = Environment.TickCount64;
                for(int i = 0; i < (int)this._shardCount; i++) {
                    foreach(KeyValuePair<long, LeaseEntry> kvp in this._shards[i]) {
                        // Süreyi thread-safe olarak oku
                        if(Volatile.Read(ref kvp.Value.ExpiryMs) < now) {
                            this._shards[i].TryRemove(kvp.Key, out _);
                        }
                    }
                }
            }
        }
        catch(OperationCanceledException) { }
    }

    public void Dispose() {
        this._cts.Cancel();
        this._cts.Dispose();
        this._timer.Dispose();

        for(int i = 0; i < this._shards.Length; i++) {
            this._shards[i].Clear();
        }
    }
}