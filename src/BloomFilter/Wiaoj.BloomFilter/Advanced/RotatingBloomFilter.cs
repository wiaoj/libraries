using System.Numerics;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Advanced;
/// <summary>
/// A time-windowed, persistent Bloom Filter that rotates underlying shards based on a Time-To-Live (TTL).
/// Automatically discards old data to maintain a clean sliding window (e.g., "Unique Users in Last 7 Days").
/// </summary>
public sealed class RotatingBloomFilter : IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly TimeSpan _shardDuration;
    private readonly DisposeState _disposeState = new();

    private Shard[] _shards;
    private long _shardCounter;

    public string Name => Configuration.Name;
    public BloomFilterConfiguration Configuration { get; }

    // Herhangi bir aktif shard kirliyse filtremiz de kirlidir (Diske yazılmalı)
    public bool IsDirty {
        get {
            var currentShards = Atomic.Read(ref _shards);
            for(int i = 0; i < currentShards.Length; i++) {
                if(currentShards[i].Filter.IsDirty) return true;
            }
            return false;
        }
    }

    // İç yapı: Filtrenin kendisi ve ne zaman çöpe gideceği (Expiration)
    private readonly record struct Shard(IPersistentBloomFilter Filter, UnixTimestamp Expiration);

    /// <summary>
    /// Initializes a rotating filter.
    /// Example: windowSize = 7 Days, shardCount = 7 means one shard per day.
    /// </summary>
    public RotatingBloomFilter(
        BloomFilterConfiguration baseConfig,
        BloomFilterContext context,
        TimeSpan windowSize,
        int shardCount) {

        Preca.ThrowIfNull(baseConfig);
        Preca.ThrowIfNull(context);
        Preca.ThrowIfNegativeOrZero(shardCount);

        Configuration = baseConfig;
        _context = context;
        _shardDuration = windowSize / shardCount;
        _shards = new Shard[shardCount];

        long itemsPerShard = (long)Math.Ceiling((double)baseConfig.ExpectedItems / shardCount);

        // ZAMAN HİZALAMASI (Time Alignment): Başlangıç noktasını pürüzsüz yap
        // Örn: Günlük shard açıyorsak, tam gece yarısına hizala.
        UnixTimestamp alignedNow = AlignTimestamp(context.TimeProvider.GetUnixTimestamp(), _shardDuration);

        // Başlangıç için shard'ları önceden ayır (Pre-allocate)
        for(int i = 0; i < shardCount; i++) {
            var expiration = alignedNow + (_shardDuration * (i + 1));
            _shards[i] = CreateShard(expiration, itemsPerShard);
        }
    }

    public bool Add(ReadOnlySpan<byte> item) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);

        // ZAMAN KONTROLÜ: Eğer aktif pencere dolduysa, yenisini aç
        EnsureActiveShard();

        _lock.EnterReadLock();
        try {
            var currentShards = Atomic.Read(ref _shards);
            // Ekleme İŞLEMİ DAİMA EN YENİ (AKTİF) SHARD'A YAPILIR
            return currentShards[^1].Filter.Add(item);
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    public bool Contains(ReadOnlySpan<byte> item) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);

        // Okuma yapmadan önce de shard'ların güncel olduğundan emin ol
        EnsureActiveShard();

        _lock.EnterReadLock();
        try {
            var currentShards = Atomic.Read(ref _shards);
            // SIFIR-ALLOCATION TARAMA: En yeni veriden en eskiye doğru tara
            for(int i = currentShards.Length - 1; i >= 0; i--) {
                if(currentShards[i].Filter.Contains(item)) return true;
            }
            return false;
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    // --- ZAMAN PENCERESİ ROTASYON MANTIĞI ---

    private void EnsureActiveShard() {
        var now = _context.TimeProvider.GetUnixTimestamp();
        var currentShards = Atomic.Read(ref _shards);

        // Hızlı, kilit-siz (lock-free) kontrol. Süre dolmadıysa direkt çık.
        if(now <= currentShards[^1].Expiration) return;

        _lock.EnterWriteLock();
        try {
            // Double-check: Başka thread bizden önce rotasyon yapmış olabilir
            currentShards = Atomic.Read(ref _shards);
            if(now <= currentShards[^1].Expiration) return;

            // Kaç tane shard'ın süresi doldu? (Eğer uygulama uzun süre kapalı kaldıysa birden fazla atlanabilir)
            int shifts = 1;
            while(now > currentShards[^1].Expiration + (_shardDuration * shifts) && shifts < currentShards.Length) {
                shifts++;
            }

            var newShards = new Shard[currentShards.Length];
            long itemsPerShard = currentShards[0].Filter.Configuration.ExpectedItems;

            // 1. Ölen Shard'ları Temizle (RAM ve Diskten at)
            for(int i = 0; i < shifts; i++) {
                var deadFilter = currentShards[i].Filter;

                // RAM'den sil
                if(deadFilter is IDisposable d) d.Dispose();

                // Diskten tamamen sil (Storage varsa) Fire-and-Forget
                if(_context.Storage != null) {
                    _ = _context.Storage.DeleteAsync(deadFilter.Name, CancellationToken.None);
                }
            }

            // 2. Kalan hayattaki shard'ları sola kaydır (Eskitiyoruz)
            int remaining = currentShards.Length - shifts;
            if(remaining > 0) {
                Array.Copy(currentShards, shifts, newShards, 0, remaining);
            }

            // 3. En sağa yepyeni Shard'lar üret (Gelecek için)
            var baseExpiration = remaining > 0 ? currentShards[^1].Expiration : AlignTimestamp(now, _shardDuration);

            for(int i = remaining; i < newShards.Length; i++) {
                var expiration = baseExpiration + (_shardDuration * (i - remaining + 1));
                newShards[i] = CreateShard(expiration, itemsPerShard);
            }

            // Atomik olarak canlıya al
            Atomic.Write(ref _shards, newShards);
        }
        finally {
            _lock.ExitWriteLock();
        }
    }

    private Shard CreateShard(UnixTimestamp expiration, long expectedItems) {
        long nextId = Interlocked.Increment(ref _shardCounter);

        var config = new BloomFilterConfiguration(
            FilterName.Parse($"{Configuration.Name.Value}_W{nextId}"),
            expectedItems,
            Configuration.ErrorRate,
            Configuration.HashSeed + (uint)nextId
        );

        // Akıllı Katman Üretici (Boyuta göre InMemory veya Sharded)
        long totalBytes = (config.SizeInBits + 7) / 8;
        IPersistentBloomFilter filter = (totalBytes > _context.Options.Lifecycle.ShardingThresholdBytes)
            ? new ShardedBloomFilter(config.WithShardCount((int)BitOperations.RoundUpToPowerOf2((uint)Math.Ceiling((double)totalBytes / _context.Options.Lifecycle.ShardingThresholdBytes))), _context)
            : new InMemoryBloomFilter(config, _context);

        return new Shard(filter, expiration);
    }

    /// <summary>
    /// Rounds down the timestamp to the nearest duration boundary.
    /// E.g., if duration is 1 day, it aligns to 00:00:00 UTC.
    /// </summary>
    private static UnixTimestamp AlignTimestamp(UnixTimestamp timestamp, TimeSpan duration) {
        long ms = timestamp.TotalMilliseconds;
        long durationMs = (long)duration.TotalMilliseconds;
        long mod = ms % durationMs;
        if(ms < 0 && mod != 0) mod += durationMs;
        return UnixTimestamp.FromMilliseconds(ms - mod);
    }

    // --- Diske Kayıt ve Yükleme İşlemleri (IPersistentBloomFilter) ---

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);
        var currentShards = Atomic.Read(ref _shards);

        // Sadece kirlenen aktif shard'ları kaydet
        var saveTasks = currentShards
            .Where(s => s.Filter.IsDirty)
            .Select(s => s.Filter.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks);
    }

    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        _disposeState.ThrowIfDisposingOrDisposed(Name);
        var currentShards = Atomic.Read(ref _shards);

        var reloadTasks = currentShards.Select(s => s.Filter.ReloadAsync(cancellationToken).AsTask());
        await Task.WhenAll(reloadTasks);
    }

    // --- Boilerplate (String/Char Overloads & Dispose & GetPopCount) ---

    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Add(buffer.Slice(0, written));
    }

    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Contains(buffer.Slice(0, written));
    }

    public long GetPopCount() {
        _lock.EnterReadLock();
        try {
            long total = 0;
            var currentShards = Atomic.Read(ref _shards);
            for(int i = 0; i < currentShards.Length; i++) total += currentShards[i].Filter.GetPopCount();
            return total;
        }
        finally {
            _lock.ExitReadLock();
        }
    }

    public void Dispose() {
        if(_disposeState.TryBeginDispose()) {
            _lock.EnterWriteLock();
            try {
                var currentShards = Atomic.Read(ref _shards);
                foreach(var shard in currentShards) {
                    if(shard.Filter is IDisposable d) d.Dispose();
                }
            }
            finally {
                _lock.ExitWriteLock();
                _lock.Dispose();
            }
            _disposeState.SetDisposed();
        }
    }
}