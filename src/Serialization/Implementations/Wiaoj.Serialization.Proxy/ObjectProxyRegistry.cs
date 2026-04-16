using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Wiaoj.Serialization.Proxy;
/// <summary>
/// Yüksek performanslı, sharded (bölümlenmiş) nesne kayıt defteri.
/// Kilit çekişmesini azaltmak için işlemci sayısına göre bölümlenmiştir.
/// </summary>
public sealed class ObjectProxyRegistry : IDisposable {
    private sealed record LeaseEntry(object Value, long ExpiryTicks);

    private readonly uint _shardCount = BitOperations.RoundUpToPowerOf2((uint)Environment.ProcessorCount);
    private readonly uint _shardMask;
    private long _nextId = 0;
    private readonly ConcurrentDictionary<long, LeaseEntry>[] _shards;
    private readonly CancellationTokenSource _cts = new();
    private readonly PeriodicTimer _timer;

    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromSeconds(10);

    public ObjectProxyRegistry() {
        this._shardMask = this._shardCount - 1;
        this._shards = new ConcurrentDictionary<long, LeaseEntry>[this._shardCount];
        for(int i = 0; i < (int)this._shardCount; i++) {
            this._shards[i] = new ConcurrentDictionary<long, LeaseEntry>(1, 1024);
        }

        this._timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        _ = StartCleanupTaskAsync();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Register(object value) {
        long id = Interlocked.Increment(ref this._nextId);
        long expiry = DateTime.UtcNow.Ticks + this.LeaseDuration.Ticks;

        int shardIndex = (int)(id & this._shardMask);
        this._shards[shardIndex].TryAdd(id, new LeaseEntry(value, expiry));
        return id;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object? Get(long id) {
        int shardIndex = (int)(id & this._shardMask);
        if(this._shards[shardIndex].TryGetValue(id, out LeaseEntry? entry)) {
            long newExpiry = DateTime.UtcNow.Ticks + this.LeaseDuration.Ticks;
            this._shards[shardIndex][id] = entry with { ExpiryTicks = newExpiry };
            return entry.Value;
        }
        return null;
    }

    private async Task StartCleanupTaskAsync() {
        try {
            while(await this._timer.WaitForNextTickAsync(this._cts.Token)) {
                long now = DateTime.UtcNow.Ticks;
                for(int i = 0; i < (int)this._shardCount; i++) {
                    foreach(KeyValuePair<long, LeaseEntry> kvp in this._shards[i]) {
                        if(kvp.Value.ExpiryTicks < now) {
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