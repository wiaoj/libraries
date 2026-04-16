using System.Drawing;
using System.Numerics;
using System.Text;
using Wiaoj.BloomFilter.Internal;
using Wiaoj.Concurrency;
using Wiaoj.Preconditions;
using Wiaoj.Primitives;
using Wiaoj.Primitives.Buffers;

namespace Wiaoj.BloomFilter.Advanced;

/// <summary>
/// A scalable, persistent Bloom Filter that automatically layers new filters when saturated.
/// </summary>
public sealed class ScalableBloomFilter: IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly GrowthRate _growthRate;
    private readonly Percentage _saturationThreshold;
    private readonly DisposeState _disposeState = new();

    // DİKKAT: Artık arayüz üzerinden tutuyoruz (InMemory veya Sharded olabilir!)
    private IPersistentBloomFilter[] _layers;

    public string Name => this.Configuration.Name;
    public BloomFilterConfiguration Configuration { get; }

    // Herhangi bir katman kirliyse ana filtre de kirlidir (Diske yazılması gerekir)
    public bool IsDirty {
        get {
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            for(int i = 0; i < currentLayers.Length; i++) {
                if(currentLayers[i].IsDirty) return true;
            }
            return false;
        }
    }

    public ScalableBloomFilter(
        BloomFilterConfiguration baseConfig,
        BloomFilterContext context,
        GrowthRate growthRate = default,
        Percentage saturationThreshold = default) {

        Preca.ThrowIfNull(baseConfig);
        Preca.ThrowIfNull(context);

        this.Configuration = baseConfig;
        this._context = context;
        this._growthRate = growthRate.Value == 0 ? GrowthRate.Double : growthRate;
        this._saturationThreshold = saturationThreshold.IsZero ? Percentage.Half : saturationThreshold;

        // İlk katmanı Provider'ın kurallarına göre oluştur (Akıllı Factory Metodu)
        this._layers = [CreateLayer(baseConfig, 0)];
    }
    private long _addCount = 0;
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);

        // 1. Contains (Arama - Tüm katmanlar)
        for(int i = layers.Length - 1; i >= 0; i--) {
            if(layers[i].Contains(item)) return false;
        }

        // 2. Ekleme (Sadece son katman)
        IPersistentBloomFilter activeLayer = layers[^1];
        bool added = activeLayer.Add(item);

        // 3. Ölçeklendirme Kontrolü (Her 1000 eklemede bir kontrol et, performansı bozma)
        if(added && Interlocked.Increment(ref _addCount) % 1000 == 0) {
            CheckAndScale(activeLayer);
        }

        return added;
    }

    private void CheckAndScale(IPersistentBloomFilter activeLayer) {
        double fillRatio = (double)activeLayer.GetPopCount() / activeLayer.Configuration.SizeInBits; 
        if(fillRatio >= this._saturationThreshold.Value) {
            ScaleUp();
        }
    }

    //public bool Add(ReadOnlySpan<byte> item) {
    //    this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

    //    this._lock.EnterReadLock();
    //    try {
    //        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
    //        for(int i = currentLayers.Length - 1; i >= 0; i--) {
    //            if(currentLayers[i].Contains(item)) return false;
    //        }
    //    }
    //    finally {
    //        this._lock.ExitReadLock();
    //    }

    //    bool needsScale = false;

    //    this._lock.EnterReadLock();
    //    try {
    //        IPersistentBloomFilter activeLayer = Atomic.Read(ref this._layers)[^1];

    //        // Sadece aktif katmana ekle!
    //        if(activeLayer.Add(item)) {
    //            // Sadece ekleme başarılıysa doluluk kontrolü yap
    //            Percentage fillRatio = Percentage.FromDouble(activeLayer.GetPopCount() / activeLayer.Configuration.SizeInBits);
    //            if(fillRatio >= this._saturationThreshold) {
    //                needsScale = true;
    //            }
    //        }
    //    }
    //    finally {
    //        this._lock.ExitReadLock();
    //    }

    //    if(needsScale) {
    //        ScaleUp();
    //    }

    //    return true;
    //}

    public bool Contains(ReadOnlySpan<byte> item) {
        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);
        // En yeni katmandan geriye doğru git (L4, L3, L2...)
        for(int i = layers.Length - 1; i >= 0; i--) {
            if(layers[i].Contains(item)) return true;
        }
        return false;
    }

    private void ScaleUp() {
        this._lock.EnterWriteLock();
        try {
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            IPersistentBloomFilter activeLayer = currentLayers[^1];

            Percentage fillRatio = Percentage.FromDouble((double)activeLayer.GetPopCount() / activeLayer.Configuration.SizeInBits);
            if(fillRatio < this._saturationThreshold) return; // Double-check

            // Nüfusu belirtilen oranda büyüt
            long newExpectedItems = (long)(activeLayer.Configuration.ExpectedItems * this._growthRate.Value);

            var newConfig = new BloomFilterConfiguration(
                FilterName.Parse($"{this.Configuration.Name.Value}_L{currentLayers.Length}"),
                newExpectedItems,
                activeLayer.Configuration.ErrorRate,
                activeLayer.Configuration.HashSeed + (uint)currentLayers.Length
            );

            // Katmanı akıllı factory ile oluştur
            IPersistentBloomFilter newLayer = CreateLayer(newConfig, currentLayers.Length);

            var newLayers = new IPersistentBloomFilter[currentLayers.Length + 1];
            Array.Copy(currentLayers, newLayers, currentLayers.Length);
            newLayers[^1] = newLayer;

            Atomic.Write(ref this._layers, newLayers);
        }
        finally {
            this._lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Akıllı Katman Fabrikası: Tıpkı BloomFilterProvider gibi, boyuta bakarak
    /// ShardedBloomFilter veya InMemoryBloomFilter döndürür.
    /// </summary>
    private IPersistentBloomFilter CreateLayer(BloomFilterConfiguration config, int layerIndex) {
        long totalBytes = (config.SizeInBits + 7) / 8;

        if(totalBytes > this._context.Options.Lifecycle.ShardingThresholdBytes) {
            double ratio = (double)totalBytes / this._context.Options.Lifecycle.ShardingThresholdBytes;
            int needed = (int)Math.Ceiling(ratio);
            int calculatedShards = (int)BitOperations.RoundUpToPowerOf2((uint)needed);

            return new ShardedBloomFilter(config.WithShardCount(calculatedShards), this._context);
        }

        return new InMemoryBloomFilter(config, this._context);
    }

    // --- Diske Kayıt ve Yükleme İşlemleri (IPersistentBloomFilter) ---

    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);

        // Sadece kirlenmiş (yeni veri eklenmiş) katmanları kaydet.
        // Paralel olarak I/O işlemlerini yap!
        IEnumerable<Task> saveTasks = currentLayers
            .Where(l => l.IsDirty)
            .Select(l => l.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks);
    }

    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
        IEnumerable<Task> reloadTasks = currentLayers.Select(l => l.ReloadAsync(cancellationToken).AsTask());

        await Task.WhenAll(reloadTasks);
    }

    // --- Boilerplate (String/Char Overloads & Dispose) ---

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
        this._lock.EnterReadLock();
        try {
            long total = 0;
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            for(int i = 0; i < currentLayers.Length; i++) total += currentLayers[i].GetPopCount();
            return total;
        }
        finally {
            this._lock.ExitReadLock();
        }
    }

    public void Dispose() {
        if(this._disposeState.TryBeginDispose()) {
            this._lock.EnterWriteLock();
            try {
                IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
                foreach(IPersistentBloomFilter? layer in currentLayers) {
                    if(layer is IDisposable disposableLayer) {
                        disposableLayer.Dispose();
                    }
                }
            }
            finally {
                this._lock.ExitWriteLock();
                this._lock.Dispose();
            }
            this._disposeState.SetDisposed();
        }
    }
}