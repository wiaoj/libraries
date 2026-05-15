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
/// A scalable, persistent Bloom Filter that automatically layers new filters when the current active layer reaches a saturation threshold.
/// This implementation allows the filter to grow dynamically while maintaining a target false positive rate.
/// </summary>
public sealed class ScalableBloomFilter: IPersistentBloomFilter, IDisposable {
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);
    private readonly BloomFilterContext _context;
    private readonly GrowthRate _growthRate;
    private readonly Percentage _saturationThreshold;
    private readonly DisposeState _disposeState = new();
     
    private IPersistentBloomFilter[] _layers;

    /// <inheritdoc/>
    public string Name => this.Configuration.Name;

    /// <inheritdoc/>
    public BloomFilterConfiguration Configuration { get; }

    /// <summary>
    /// Gets a value indicating whether any of the underlying layers have been modified and require persistence.
    /// </summary>
    public bool IsDirty {
        get {
            IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
            for(int i = 0; i < currentLayers.Length; i++) {
                if(currentLayers[i].IsDirty) return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ScalableBloomFilter"/> class.
    /// </summary>
    /// <param name="baseConfig">The initial configuration for the first layer.</param>
    /// <param name="context">The shared context containing logging, storage, and factory services.</param>
    /// <param name="growthRate">The multiplier used to increase capacity for each new layer. Defaults to <see cref="GrowthRate.Double"/>.</param>
    /// <param name="saturationThreshold">The fill ratio at which a new layer is triggered. Defaults to 50%.</param>
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
         
        this._layers = [CreateLayer(baseConfig, 0)];
    }

    private long _addCount = 0;

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<byte> item) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);

        // 1. Search - Check all layers from newest to oldest
        for(int i = layers.Length - 1; i >= 0; i--) {
            if(layers[i].Contains(item)) return false;
        }

        // 2. Add - Only add to the latest (active) layer
        IPersistentBloomFilter activeLayer = layers[^1];
        bool added = activeLayer.Add(item);

        // 3. Scaling Check - Periodically check for saturation (every 1000 additions to minimize overhead)
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

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<byte> item) {
        IPersistentBloomFilter[] layers = Atomic.Read(ref this._layers);
        // Search from the newest layer backwards (L4, L3, L2...)
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
            if(fillRatio < this._saturationThreshold) return; // Double-check under lock

            // Increase expected items by the growth rate
            long newExpectedItems = (long)(activeLayer.Configuration.ExpectedItems * this._growthRate.Value);

            var newConfig = this._context.ConfigFactory.Create(
                FilterName.Parse($"{this.Configuration.Name.Value}_L{currentLayers.Length}"),
                newExpectedItems,
                activeLayer.Configuration.ErrorRate,
                activeLayer.Configuration.HashSeed + (uint)currentLayers.Length
            );

            // Create new layer using the intelligent factory
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
    /// Intelligent Layer Factory: Similar to BloomFilterProvider, determines whether to 
    /// create a ShardedBloomFilter or InMemoryBloomFilter based on the calculated size.
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

    /// <inheritdoc/>
    public async ValueTask SaveAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);

        // Only save layers that have been modified (dirty state).
        // Perform I/O operations in parallel.
        IEnumerable<Task> saveTasks = currentLayers
            .Where(l => l.IsDirty)
            .Select(l => l.SaveAsync(cancellationToken).AsTask());

        await Task.WhenAll(saveTasks);
    }

    /// <inheritdoc/>
    public async ValueTask ReloadAsync(CancellationToken cancellationToken = default) {
        this._disposeState.ThrowIfDisposingOrDisposed(this.Name);

        IPersistentBloomFilter[] currentLayers = Atomic.Read(ref this._layers);
        IEnumerable<Task> reloadTasks = currentLayers.Select(l => l.ReloadAsync(cancellationToken).AsTask());

        await Task.WhenAll(reloadTasks);
    }

    /// <inheritdoc/>
    public bool Add(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Add(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
    public bool Contains(ReadOnlySpan<char> item) {
        int maxBytes = Encoding.UTF8.GetMaxByteCount(item.Length);
        using ValueBuffer<byte> buffer = new(maxBytes, stackalloc byte[256]);
        int written = Encoding.UTF8.GetBytes(item, buffer.Span);
        return Contains(buffer.Slice(0, written));
    }

    /// <inheritdoc/>
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

    /// <summary>
    /// Releases all resources used by the Scalable Bloom Filter and its underlying layers.
    /// </summary>
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